using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil
{
    public class Cfg<T> : IDisposable
        where T: class, ICloneable, new()
    {
        public object ConfigLock = new object();
        public T Config { get; set; }

        public string FileName { get; }
        public string DirName { get; }

        public Cfg(T default_config = null, string filename = null, string header_str = null)
        {
            if (default_config == null) default_config = new T();
            if (filename.IsEmpty()) filename = "@" + default_config.GetType().ToString().MakeSafeFileName() + ".config";
            this.FileName = IO.InnerFilePath(filename);
            this.DirName = this.FileName.GetDirectoryName();
            IO.MakeDirIfNotExists(this.DirName);
            if (header_str.IsEmpty())
            {
                header_str = @"# Configuration file
# YAML format
# 

";
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~Cfg() {
        //   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //   Dispose(false);
        // }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}

