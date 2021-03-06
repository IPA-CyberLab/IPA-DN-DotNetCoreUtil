﻿using System;
using System.Threading;
using System.Linq;
using System.Linq.Expressions;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Data.SqlClient;
//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib;
using Dapper.Contrib.Extensions;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.Basic
{
    public class EasyTable : TableAttribute
    {
        public EasyTable(string tableName) : base(tableName) { }
    }
    public class EasyKey : KeyAttribute { }
    public class EasyManualKey : ExplicitKeyAttribute { }
    public class EasyReadOnly : WriteAttribute { public EasyReadOnly() : base(false) { } }

    public class DapperColumn : Attribute
    {
        public string Name;
        public DapperColumn(string name) { this.Name = name; }
    }

    public class DbConsoleDebugPrinterProvider : ILoggerProvider
    {
        Ref<bool> enable_console_logger;

        public DbConsoleDebugPrinterProvider(Ref<bool> enable_console_logger)
        {
            this.enable_console_logger = enable_console_logger;
        }

        public ILogger CreateLogger(string categoryName) => new DbConsoleDebugPrinter(this.enable_console_logger);
        public void Dispose() { }
    }

    public class DbConsoleDebugPrinter : ILogger
    {
        Ref<bool> enable_console_logger;

        public DbConsoleDebugPrinter(Ref<bool> enable_console_logger)
        {
            this.enable_console_logger = enable_console_logger;
        }

        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel)
        {
            if (enable_console_logger == false) return false;

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Information:
                case LogLevel.None:
                    return false;
            }
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (eventId.ToString().IndexOf("CommandExecuting", StringComparison.InvariantCultureIgnoreCase) == -1)
            {
                return;
            }
            StringWriter w = new StringWriter();
            w.WriteLine("----------");
            if (state != null) w.Write($"State: {state}");
            if (exception != null) w.Write($", Exception: {exception.Message}");
            w.WriteLine();
            w.WriteLine("----------");
            string str = w.ToString();
            Dbg.WriteLine(w.ToString().TrimCrlf());
        }
    }

    // データベース値
    public class DatabaseValue
    {
        public bool IsNull
        {
            get
            {
                if (Object == null) return true;
                if (Object == (object)DBNull.Value) return true;
                return false;
            }
        }

        public DateTime DateTime => ((DateTime)Object).NormalizeDateTime();
        public DateTimeOffset DateTimeOffset => ((DateTimeOffset)Object).NormalizeDateTimeOffset();
        public string String => (string)Object;
        public double Double => (double)Object;
        public int Int => (int)Object;
        public uint UInt => (uint)Object;
        public long Int64 => (long)Object;
        public ulong UInt64 => (ulong)Object;
        public bool Bool => (bool)Object;
        public byte[] Data => (byte[])Object;
        public short Short => (short)Object;
        public ushort UShort => (ushort)Object;
        public byte Byte => (byte)Object;
        public sbyte SByte => (sbyte)Object;

        public object Object { get; }

        public DatabaseValue(object value)
        {
            this.Object = value;
        }

        public override string ToString()
        {
            return this.Object.ToString();
        }
    }

    // 行
    public class Row
    {
        public readonly DatabaseValue[] ValueList;
        public readonly string[] FieldList;

        public Row(DatabaseValue[] ValueList, string[] FieldList)
        {
            this.ValueList = ValueList;
            this.FieldList = FieldList;
        }

        public DatabaseValue this[string name]
        {
            get
            {
                int i;
                for (i = 0; i < this.FieldList.Length; i++)
                {
                    if (this.FieldList[i].Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return this.ValueList[i];
                    }
                }

                throw new ApplicationException("Field \"" + name + "\" not found.");
            }
        }

        public override string ToString()
        {
            string[] strs = new string[this.ValueList.Length];
            int i;
            for (i = 0; i < this.ValueList.Length; i++)
            {
                strs[i] = this.ValueList[i].ToString();
            }

            return Str.CombineStringArray(strs, ", ");
        }
    }

    // データ
    public class Data : IEnumerable
    {
        public Row[] RowList { get; private set; }
        public string[] FieldList { get; private set; }

        public Data() { }

        public Data(Database db)
        {
            DbDataReader r = db.DataReader;

            int i;
            int num = r.FieldCount;

            List<string> fields_list = new List<string>();

            for (i = 0; i < num; i++)
            {
                fields_list.Add(r.GetName(i));
            }

            this.FieldList = fields_list.ToArray();

            List<Row> row_list = new List<Row>();
            while (db.ReadNext())
            {
                DatabaseValue[] values = new DatabaseValue[this.FieldList.Length];

                for (i = 0; i < this.FieldList.Length; i++)
                {
                    values[i] = db[this.FieldList[i]];
                }

                row_list.Add(new Row(values, this.FieldList));
            }

            this.RowList = row_list.ToArray();
        }

        public async Task ReadFromDbAsync(Database db)
        {
            DbDataReader r = db.DataReader;

            int i;
            int num = r.FieldCount;

            List<string> fields_list = new List<string>();

            for (i = 0; i < num; i++)
            {
                fields_list.Add(r.GetName(i));
            }

            this.FieldList = fields_list.ToArray();

            List<Row> row_list = new List<Row>();
            while (await db.ReadNextAsync())
            {
                DatabaseValue[] values = new DatabaseValue[this.FieldList.Length];

                for (i = 0; i < this.FieldList.Length; i++)
                {
                    values[i] = db[this.FieldList[i]];
                }

                row_list.Add(new Row(values, this.FieldList));
            }

            this.RowList = row_list.ToArray();
        }

        public IEnumerator GetEnumerator()
        {
            int i;
            for (i = 0; i < this.RowList.Length; i++)
            {
                yield return this.RowList[i];
            }
        }
    }

    // Using トランザクション
    public class UsingTran : IDisposable
    {
        Database db;

        internal UsingTran(Database db)
        {
            this.db = db;
        }

        object lock_obj = new object();

        public void Commit()
        {
            this.db.Commit();
        }

        public void Dispose()
        {
            Database db = null;
            lock (lock_obj)
            {
                if (this.db != null)
                {
                    db = this.db;
                    this.db = null;
                }
            }

            if (db != null)
            {
                db.Cancel();
            }
        }
    }

    // デッドロック再試行設定
    public class DeadlockRetryConfig
    {
        public readonly int RetryAverageInterval;
        public readonly int RetryCount;

        public DeadlockRetryConfig(int RetryAverageInterval, int RetryCount)
        {
            this.RetryAverageInterval = RetryAverageInterval;
            this.RetryCount = RetryCount;
        }
    }

    // データベースアクセス
    public class Database : IDisposable
    {
        static Database()
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public DbConnection Connection { get; private set; } = null;
        public DbTransaction Transaction { get; private set; } = null;
        public DbDataReader DataReader { get; private set; } = null;
        public bool IsOpened { get; private set; } = false;

        public const int DefaultCommandTimeoutSecs = 60;
        public int CommandTimeoutSecs { get; set; } = DefaultCommandTimeoutSecs;

        public static readonly DeadlockRetryConfig DefaultDeadlockRetryConfig = new DeadlockRetryConfig(4000, 10);

        public DeadlockRetryConfig DeadlockRetryConfig = DefaultDeadlockRetryConfig;

        // コンストラクタ
        public Database(string sqlServerConnectionString)
        {
            Connection = new SqlConnection(sqlServerConnectionString);
        }

        public Database(DbConnection dbConnection)
        {
            Connection = dbConnection;
        }

        public void EnsureOpen() => EnsureOpenAsync().Wait();

        AsyncLock OpenCloseLock = new AsyncLock();

        public async Task EnsureOpenAsync()
        {
            if (IsOpened) return;
            using (await OpenCloseLock.LockWithAwait())
            {
                if (IsOpened == false)
                {
                    await Connection.OpenAsync();
                    IsOpened = true;
                }
            }
        }

        // バルク書き込み
        public void SqlBulkWrite(string tableName, DataTable dt)
        {
            EnsureOpen();

            using (SqlBulkCopy bc = new SqlBulkCopy((SqlConnection)this.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)Transaction))
            {
                bc.BulkCopyTimeout = CommandTimeoutSecs;
                bc.DestinationTableName = tableName;
                bc.WriteToServer(dt);
            }
        }

        public async Task SqlBulkWriteAsync(string tableName, DataTable dt)
        {
            await EnsureOpenAsync();

            using (SqlBulkCopy bc = new SqlBulkCopy((SqlConnection)this.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)Transaction))
            {
                bc.BulkCopyTimeout = CommandTimeoutSecs;
                bc.DestinationTableName = tableName;
                await bc.WriteToServerAsync(dt);
            }
        }

        // クエリの実行
        public void Query(string commandStr, params object[] args)
        {
            EnsureOpen();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            DataReader = cmd.ExecuteReader();
        }

        public async Task QueryAsync(string commandStr, params object[] args)
        {
            await EnsureOpenAsync();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            DataReader = await cmd.ExecuteReaderAsync();
        }

        static object DapperTypeMapLock = new object();
        static HashSet<Type> DapperInstalledTypes = new HashSet<Type>();
        static void EnsureDapperTypeMapping(Type t)
        {
            if (t == null) return;
            lock (DapperTypeMapLock)
            {
                if (DapperInstalledTypes.Contains(t) == false)
                {
                    DapperInstalledTypes.Add(t);
                    Dapper.SqlMapper.SetTypeMap(t,
                        new CustomPropertyTypeMap(t, (type, columnName) =>
                        {
                            var properties = type.GetProperties();

                            // UserName == USER_NAME
                            var c1 = properties.SingleOrDefault(p => p.Name.IsSamei(columnName)
                                || p.GetCustomAttributes(true).OfType<DapperColumn>().Any(a => a.Name.IsSamei(columnName)));
                            if (c1 != null) return c1;

                            int i = columnName.IndexOf("_");
                            if (i >= 2)
                            {
                                // UserName == USERS_USERNAME
                                string columnName2 = columnName.Substring(i + 1);

                                var c2 = properties.SingleOrDefault(p => p.Name.IsSamei(columnName2)
                                    || p.GetCustomAttributes(true).OfType<DapperColumn>().Any(a => a.Name.IsSamei(columnName2)));
                                if (c2 != null) return c2;
                            }

                            return null;
                        }));
                }
            }
        }

        async Task<CommandDefinition> SetupDapperAsync(string commandStr, object param, Type type = null)
        {
            await EnsureOpenAsync();

            EnsureDapperTypeMapping(type);

            if (param != null)
                EnsureDapperTypeMapping(param.GetType());

            CommandDefinition cmd = new CommandDefinition(commandStr, param, this.Transaction);

            return cmd;
        }

        public async Task<T> GetOrInsertIfEmptyAsync<T>(string selectStr, object selectParam, string insertStr, object insertParam, string newCreatedRowSelectWithIdCmd)
        {
            IEnumerable<T> ret = await QueryAsync<T>(selectStr, selectParam);
            T t = ret.SingleOrDefault();
            if (t == default)
            {
                await ExecuteScalarAsync<T>(insertStr, insertParam);

                long newId = await this.GetLastID64Async();

                ret = await QueryAsync<T>(newCreatedRowSelectWithIdCmd, new { id = newId });

                t = ret.Single();
            }

            return t;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string commandStr, object param = null)
            => await Connection.QueryAsync<T>(await SetupDapperAsync(commandStr, param, typeof(T)));

        public async Task<int> ExecuteAsync(string commandStr, object param = null)
            => await Connection.ExecuteAsync(await SetupDapperAsync(commandStr, param, null));

        public async Task<T> ExecuteScalarAsync<T>(string commandStr, object param = null)
            => (T)(await Connection.ExecuteScalarAsync(await SetupDapperAsync(commandStr, param, null)));

        public IEnumerable<T> Query<T>(string commandStr, object param = null)
            => QueryAsync<T>(commandStr, param).Result;

        public int Execute(string commandStr, object param = null)
            => ExecuteAsync(commandStr, param).Result;

        public T ExecuteScalar<T>(string commandStr, object param = null)
            => ExecuteScalarAsync<T>(commandStr, param).Result;


        public async Task<T> EasyGetAsync<T>(dynamic id, bool throwErrorIfNotFound = false) where T : class
        {
            await EnsureOpenAsync();
            EnsureDapperTypeMapping(typeof(T));

            var ret = await SqlMapperExtensions.GetAsync<T>(Connection, id, Transaction);

            if (throwErrorIfNotFound && ret == null)
                throw new KeyNotFoundException();

            return ret;
        }

        public async Task<IEnumerable<T>> EasyGetAllAsync<T>(bool throwErrorIfNotFound = false) where T : class
        {
            await EnsureOpenAsync();
            EnsureDapperTypeMapping(typeof(T));

            var ret = await SqlMapperExtensions.GetAllAsync<T>(Connection, Transaction);

            if (throwErrorIfNotFound && ret.Count() == 0)
                throw new KeyNotFoundException();

            return ret;
        }

        public async Task<int> EasyInsertAsync<T>(T data) where T : class
        {
            await EnsureOpenAsync();
            EnsureDapperTypeMapping(typeof(T));

            return await SqlMapperExtensions.InsertAsync<T>(Connection, data, Transaction);
        }

        public async Task<bool> EasyUpdateAsync<T>(T data, bool throwErrorIfNotFound = false) where T : class
        {
            await EnsureOpenAsync();
            EnsureDapperTypeMapping(typeof(T));

            bool ret = await SqlMapperExtensions.UpdateAsync<T>(Connection, data, Transaction);

            if (throwErrorIfNotFound && ret == false)
                throw new KeyNotFoundException();

            return ret;
        }

        public async Task<bool> EasyDeleteAsync<T>(T data, bool throwErrorIfNotFound = false) where T : class
        {
            await EnsureOpenAsync();
            EnsureDapperTypeMapping(typeof(T));

            bool ret = await SqlMapperExtensions.DeleteAsync<T>(Connection, data, Transaction);

            if (throwErrorIfNotFound && ret == false)
                throw new KeyNotFoundException();

            return ret;
        }

        public IEnumerable<T> EasyGetAll<T>(bool throwErrorIfNotFound = false) where T : class
            => EasyGetAllAsync<T>(throwErrorIfNotFound).Result;

        public int EasyInsert<T>(T data) where T : class
            => EasyInsertAsync(data).Result;

        public bool EasyUpdate<T>(T data, bool throwErrorIfNotFound = false) where T : class
            => EasyUpdateAsync(data, throwErrorIfNotFound).Result;

        public bool EasyDelete<T>(T data, bool throwErrorIfNotFound = false) where T : class
            => EasyDeleteAsync(data, throwErrorIfNotFound).Result;

        public int QueryWithNoReturn(string commandStr, params object[] args)
        {
            EnsureOpen();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            return cmd.ExecuteNonQuery();
        }

        public async Task<int> QueryWithNoReturnAsync(string commandStr, params object[] args)
        {
            await EnsureOpenAsync();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            return await cmd.ExecuteNonQueryAsync();
        }

        public DatabaseValue QueryWithValue(string commandStr, params object[] args)
        {
            EnsureOpen();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            return new DatabaseValue(cmd.ExecuteScalar());
        }

        public async Task<DatabaseValue> QueryWithValueAsync(string commandStr, params object[] args)
        {
            await EnsureOpenAsync();

            closeQuery();
            DbCommand cmd = buildCommand(commandStr, args);

            return new DatabaseValue(await cmd.ExecuteScalarAsync());
        }

        // 値の取得
        public DatabaseValue this[string name]
        {
            get
            {
                object o = DataReader[name];

                return new DatabaseValue(o);
            }
        }

        // 最後に挿入した ID
        public int LastID
        {
            get
            {
                EnsureOpen();
                return (int)((decimal)this.QueryWithValue("SELECT @@@@IDENTITY").Object);
            }
        }
        public long LastID64
        {
            get
            {
                EnsureOpen();
                return (long)((decimal)this.QueryWithValue("SELECT @@@@IDENTITY").Object);
            }
        }

        public async Task<int> GetLastIDAsync()
        {
            await EnsureOpenAsync();
            return (int)((decimal)((await this.QueryWithValueAsync("SELECT @@@@IDENTITY")).Object));
        }

        public async Task<long> GetLastID64Async()
        {
            await EnsureOpenAsync();
            return (long)((decimal)((await this.QueryWithValueAsync("SELECT @@@@IDENTITY")).Object));
        }


        // すべて読み込み
        public Data ReadAllData()
        {
            return new Data(this);
        }

        public async Task<Data> ReadAllDataAsync()
        {
            Data ret = new Data();
            await ret.ReadFromDbAsync(this);
            return ret;
        }

        // 次の行の取得
        public bool ReadNext()
        {
            if (DataReader == null)
            {
                return false;
            }
            if (DataReader.Read() == false)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ReadNextAsync()
        {
            if (DataReader == null)
            {
                return false;
            }
            if ((await DataReader.ReadAsync()) == false)
            {
                return false;
            }

            return true;
        }

        // クエリの終了
        void closeQuery()
        {
            if (DataReader != null)
            {
                DataReader.Close();
                DataReader.Dispose();
                DataReader = null;
            }
        }

        // コマンドの構築
        DbCommand buildCommand(string commandStr, params object[] args)
        {
            StringBuilder b = new StringBuilder();
            int i, len, n;
            len = commandStr.Length;
            List<DbParameter> dbParams = new List<DbParameter>();

            DbCommand cmd = Connection.CreateCommand();

            n = 0;
            for (i = 0; i < len; i++)
            {
                char c = commandStr[i];

                if (c == '@')
                {
                    if ((commandStr.Length > (i + 1)) && commandStr[i + 1] == '@')
                    {
                        b.Append(c);
                        i++;
                    }
                    else
                    {
                        string argName = "@ARG_" + n;
                        b.Append(argName);

                        DbParameter p = buildParameter(cmd, argName, args[n++]);
                        dbParams.Add(p);
                    }
                }
                else
                {
                    b.Append(c);
                }
            }

            foreach (DbParameter p in dbParams)
            {
                cmd.Parameters.Add(p);
            }

            if (Transaction != null)
                cmd.Transaction = Transaction;

            cmd.CommandText = b.ToString();
            cmd.CommandTimeout = this.CommandTimeoutSecs;

            return cmd;
        }

        // オブジェクトを SQL パラメータに変換
        DbParameter buildParameter(DbCommand cmd, string name, object o)
        {
            Type t = null;

            try
            {
                t = o.GetType();
            }
            catch
            {
            }

            if (o == null)
            {
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = DBNull.Value;
                return p;
            }
            else if (t == typeof(System.String))
            {
                string s = (string)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.String;
                p.Size = s.Length;
                p.Value = s;
                return p;
            }
            else if (t == typeof(System.Int16) || t == typeof(System.UInt16))
            {
                short s = (short)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Int16;
                p.Value = s;
                return p;
            }
            else if (t == typeof(System.Byte))
            {
                byte b = (byte)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Byte;
                p.Value = b;
                return p;
            }
            else if (t == typeof(System.Int32) || t == typeof(System.UInt32))
            {
                int i = (int)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Int32;
                p.Value = i;
                return p;
            }
            else if (t == typeof(System.Int64) || t == typeof(System.UInt64))
            {
                long i = (long)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Int64;
                p.Value = i;
                return p;
            }
            else if (t == typeof(System.Boolean))
            {
                bool b = (bool)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Boolean;
                p.Value = b;
                return p;
            }
            else if (t == typeof(System.Byte[]))
            {
                byte[] b = (byte[])o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.Binary;
                p.Value = b;
                p.Size = b.Length;
                return p;
            }
            else if (t == typeof(System.DateTime))
            {
                DateTime d = (DateTime)o;
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.DateTime;
                p.Value = d;
                return p;
            }
            else if (t == typeof(System.DateTimeOffset))
            {
                DateTimeOffset d = (DateTimeOffset)o;
                d = d.NormalizeDateTimeOffset();
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = DbType.DateTimeOffset;
                p.Value = d;
                return p;
            }

            throw new ArgumentException($"Unsupported type: '{t.Name}'");
        }

        // リソースの解放
        public void Dispose()
        {
            closeQuery();
            Cancel();
            if (Connection != null)
            {
                IsOpened = false;
                Connection.Close();
                Connection.Dispose();
                Connection = null;
            }
        }

        public delegate bool TransactionalTask();
        public delegate void TransactionalReadonlyTask();

        public delegate Task<bool> TransactionalTaskAsync();
        public delegate Task TransactionalReadOnlyTaskAsync();

        // トランザクションの実行 (匿名デリゲートを用いた再試行処理も実施)
        public void Tran(TransactionalTask task) => Tran(IsolationLevel.Serializable, null, task);
        public void Tran(IsolationLevel iso, TransactionalTask task) => Tran(iso, null, task);
        public void Tran(IsolationLevel iso, DeadlockRetryConfig retry_config, TransactionalTask task)
        {
            EnsureOpen();
            if (retry_config == null)
            {
                retry_config = this.DeadlockRetryConfig;
            }

            int num_retry = 0;

            LABEL_RETRY:
            try
            {
                using (UsingTran u = this.UsingTran(iso))
                {
                    if (task())
                    {
                        u.Commit();
                    }
                }
            }
            catch (SqlException sqlex)
            {
                if (sqlex.Number == 1205)
                {
                    // デッドロック発生
                    num_retry++;
                    if (num_retry <= retry_config.RetryCount)
                    {
                        Kernel.SleepThread(Secure.Rand31i() % retry_config.RetryAverageInterval);

                        goto LABEL_RETRY;
                    }

                    throw;
                }
                else
                {
                    throw;
                }
            }
        }

        public void TranReadSnapshot(TransactionalReadonlyTask task)
        {
            Tran(IsolationLevel.Snapshot, () =>
            {
                task();
                return false;
            });
        }

        public Task TranAsync(TransactionalTaskAsync task) => TranAsync(IsolationLevel.Serializable, null, task);
        public Task TranAsync(IsolationLevel iso, TransactionalTaskAsync task) => TranAsync(iso, null, task);
        public async Task TranAsync(IsolationLevel iso, DeadlockRetryConfig retry_config, TransactionalTaskAsync task)
        {
            await EnsureOpenAsync();
            if (retry_config == null)
            {
                retry_config = this.DeadlockRetryConfig;
            }

            int num_retry = 0;

            LABEL_RETRY:
            try
            {
                using (UsingTran u = this.UsingTran(iso))
                {
                    if (await task())
                    {
                        u.Commit();
                    }
                }
            }
            catch (SqlException sqlex)
            {
                if (sqlex.Number == 1205)
                {
                    // デッドロック発生
                    num_retry++;
                    if (num_retry <= retry_config.RetryCount)
                    {
                        await Task.Delay(Secure.Rand31i() % retry_config.RetryAverageInterval);

                        goto LABEL_RETRY;
                    }

                    throw;
                }
                else
                {
                    throw;
                }
            }
        }

        public Task TranReadSnapshotAsync(TransactionalReadOnlyTaskAsync task)
        {
            return TranAsync(IsolationLevel.Snapshot, async () =>
            {
                await task();
                return false;
            });
        }

        // トランザクションの開始 (UsingTran オブジェクト作成)
        public UsingTran UsingTran(IsolationLevel iso = IsolationLevel.Unspecified)
        {
            UsingTran t = new UsingTran(this);

            Begin(iso);

            return t;
        }

        // トランザクションの開始
        public void Begin(IsolationLevel iso = IsolationLevel.Unspecified)
        {
            EnsureOpen();

            closeQuery();

            if (iso == IsolationLevel.Unspecified)
            {
                Transaction = Connection.BeginTransaction();
            }
            else
            {
                Transaction = Connection.BeginTransaction(iso);
            }
        }

        // トランザクションのコミット
        public void Commit()
        {
            if (Transaction == null)
            {
                return;
            }

            closeQuery();
            Transaction.Commit();
            Transaction.Dispose();
            Transaction = null;
        }

        // トランザクションのロールバック
        public void Cancel()
        {
            if (Transaction == null)
            {
                return;
            }

            closeQuery();
            Transaction.Rollback();
            Transaction.Dispose();
            Transaction = null;
        }

        // データベースのテーブルのクラスで値を上書きする
        public static T DbOverwriteValues<T>(T dst, T src)
        {
            return (T)DbOverwriteValues((object)dst, (object)src);
        }
        public static object DbOverwriteValues(object dst, object src)
        {
            if (dst.GetType() != src.GetType())
            {
                throw new ApplicationException("DbOverwriteValues: dst.GetType() != src.GetType()");
            }
            object ret = dst.ClonePublics();
            Type t = dst.GetType();
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in props)
            {
                var ptype = p.PropertyType;
                object value = p.GetValue(src);
                if (value != null)
                {
                    bool ok = true;
                    if (ptype == typeof(DateTime))
                    {
                        DateTime d = (DateTime)p.GetValue(src);
                        if (Util.IsZero(d))
                        {
                            ok = false;
                        }
                    }
                    else if (ptype == typeof(DateTimeOffset))
                    {
                        DateTimeOffset d = (DateTimeOffset)p.GetValue(src);
                        if (Util.IsZero(d))
                        {
                            ok = false;
                        }
                    }
                    else if (ptype == typeof(int))
                    {
                        int i = (int)p.GetValue(src);
                        if (i == 0) ok = false;
                    }
                    else if (ptype == typeof(long))
                    {
                        long i = (long)p.GetValue(src);
                        if (i == 0) ok = false;
                    }
                    else if (ptype == typeof(decimal))
                    {
                        decimal i = (decimal)p.GetValue(src);
                        if (i == 0) ok = false;
                    }
                    else if (ptype == typeof(double))
                    {
                        double i = (double)p.GetValue(src);
                        if (i == 0) ok = false;
                    }
                    else if (ptype == typeof(float))
                    {
                        float i = (float)p.GetValue(src);
                        if (i == 0) ok = false;
                    }

                    if (ok)
                    {
                        p.SetValue(ret, value);
                    }
                }
            }

            return ret;
        }

        // データベースのテーブルのクラスで Non NULL を強制する
        public static object DbEnforceNonNull(object obj)
        {
            if (obj == null) return null;

            Type t = obj.GetType();

            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in props)
            {
                var ptype = p.PropertyType;
                if (ptype.IsNullable() == false)
                {
                    if (ptype == typeof(string))
                    {
                        string s = (string)p.GetValue(obj);
                        if (s == null) p.SetValue(obj, "");
                    }
                    else if (ptype == typeof(DateTime))
                    {
                        DateTime d = (DateTime)p.GetValue(obj);
                        if (d.IsZeroDateTime()) p.SetValue(obj, Util.ZeroDateTimeValue);
                    }
                    else if (ptype == typeof(DateTimeOffset))
                    {
                        DateTimeOffset d = (DateTimeOffset)p.GetValue(obj);
                        if (d.IsZeroDateTime()) p.SetValue(obj, Util.ZeroDateTimeOffsetValue);
                    }
                }
            }

            return obj;
        }
    }

}
