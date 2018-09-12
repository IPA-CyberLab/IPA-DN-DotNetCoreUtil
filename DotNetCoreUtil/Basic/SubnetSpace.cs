﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.Basic
{
    public class SubnetSpaceSubnet
    {
        public IPAddr Address;
        public int SubnetLength;
        public List<object> DataList;

        [JsonIgnore]
        public object DataFirst { get => this.DataList.GetFirstOrNull(); }

        internal List<(int sort_key, object data)> tmp_sort_list = new List<(int sort_key, object data)>();

        int hash_code;

        public SubnetSpaceSubnet() { }

        public SubnetSpaceSubnet(IPAddr address, int subnet_len, List<object> data_list = null)
        {
            this.Address = address;
            this.SubnetLength = subnet_len;
            this.DataList = data_list;

            if (this.DataList == null)
            {
                this.DataList = new List<object>();
            }

            if (this.SubnetLength > FullRoute.GetMaxSubnetSize(Address.AddressFamily))
            {
                throw new ApplicationException("subnet_len is too large.");
            }

            byte[] addr_bytes = this.Address.GetBytes();

            if (addr_bytes.Length == 4)
            {
                hash_code = BitConverter.ToInt32(addr_bytes, 0);
                hash_code ^= SubnetLength;
            }
            else if (addr_bytes.Length == 16)
            {
                hash_code = 0;

                hash_code ^= BitConverter.ToInt32(addr_bytes, 0);
                hash_code ^= BitConverter.ToInt32(addr_bytes, 4);
                hash_code ^= BitConverter.ToInt32(addr_bytes, 8);
                hash_code ^= BitConverter.ToInt32(addr_bytes, 12);
                hash_code ^= SubnetLength;
            }
            else
            {
                throw new ApplicationException("addr_bytes.Length");
            }
        }

        public ulong CalcNumIPs()
        {
            if (Address.AddressFamily == AddressFamily.InterNetwork)
            {
                return (ulong)(1UL << (32 - this.SubnetLength));
            }
            else
            {
                int v = 64 - this.SubnetLength;
                if (v < 0)
                {
                    v = 0;
                }
                return (ulong)(1UL << v);
            }
        }

        public override string ToString() => this.Address.ToString() + "/" + this.SubnetLength.ToString();

        public override int GetHashCode() => hash_code;

        public override bool Equals(object obj)
        {
            SubnetSpaceSubnet other = obj as SubnetSpaceSubnet;
            if (other == null) return false;
            return this.Address.Equals(other.Address) && (this.SubnetLength == other.SubnetLength);
        }

        public string GetBinaryString()
            => this.Address.GetBinaryString().Substring(0, this.SubnetLength);

        public byte[] GetBinaryBytes()
            => Util.CopyByte(this.Address.GetBinaryBytes(), 0, this.SubnetLength);

        public bool Contains(IPAddr target)
        {
            if (this.Address.AddressFamily != target.AddressFamily) return false;

            string target_str = target.GetBinaryString();
            string subnet_str = this.GetBinaryString();

            return target_str.StartsWith(subnet_str);
        }

        public static int CompareBySubnetLength(SubnetSpaceSubnet x, SubnetSpaceSubnet y)
        {
            return x.SubnetLength.CompareTo(y.SubnetLength);
        }
    }

    public class SubnetSpace
    {
        public AddressFamily AddressFamily;
        public RadixTrie Trie;

        object lockobject = new object();

        public SubnetSpace() { }

        public SubnetSpace(AddressFamily address_family)
        {
            this.AddressFamily = address_family;
        }

        // サブネット情報を投入する
        public void SetSubnets((IPAddress address, int subnet_length, object data, int data_sort_key)[] items)
        {
            // 重複するものを 1 つにまとめる
            Distinct<SubnetSpaceSubnet> distinct = new Distinct<SubnetSpaceSubnet>();

            foreach (var item in items)
            {
                SubnetSpaceSubnet s = new SubnetSpaceSubnet(IPAddr.FromAddress(item.address), item.subnet_length);

                s = distinct.AddOrGet(s);

                s.tmp_sort_list.Add((item.data_sort_key, item.data));
            }

            SubnetSpaceSubnet[] subnets = distinct.Values;

            foreach (SubnetSpaceSubnet subnet in subnets)
            {
                // tmp_sort_list の内容を sort_key に基づきソートする
                subnet.tmp_sort_list.Sort((a, b) =>
                {
                    return a.sort_key.CompareTo(b.sort_key);
                });

                // ソート済みオブジェクトを順に保存する
                subnet.DataList = new List<object>();
                foreach (var a in subnet.tmp_sort_list)
                {
                    subnet.DataList.Add(a.data);
                }
            }

            List<SubnetSpaceSubnet> subnets_list = subnets.ToList();

            subnets_list.Sort(SubnetSpaceSubnet.CompareBySubnetLength);

            var trie = new RadixTrie();

            foreach (var subnet in subnets_list)
            {
                var node = trie.Insert(subnet.GetBinaryBytes());

                node.Object = subnet;
            }

            lock (lockobject)
            {
                this.Trie = trie;
            }
        }

        // 検索をする
        public SubnetSpaceSubnet Lookup(IPAddress address)
        {
            if (address.AddressFamily != this.AddressFamily)
            {
                throw new ApplicationException("addr.AddressFamily != this.AddressFamily");
            }

            RadixTrie trie = null;
            lock (lockobject)
            {
                trie = this.Trie;
            }
            if (trie == null)
            {
                return null;
            }

            byte[] key = IPAddr.FromAddress(address).GetBinaryBytes();
            RadixNode n = trie.Lookup(key);
            if (n == null)
            {
                return null;
            }

            n = n.TraverseParentNonNull();
            if (n == null)
            {
                return null;
            }

            SubnetSpaceSubnet ret = (SubnetSpaceSubnet)n.Object;

            return ret;
        }
    }
}

