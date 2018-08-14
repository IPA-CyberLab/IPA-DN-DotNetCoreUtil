using System;
using System.Threading;
using System.Linq;
using System.Linq.Expressions;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
using System.Drawing;

using IPA.DN.CoreUtil;
using IPA.DN.CoreUtil.Helper.Basic;

namespace DotNetCoreUtilTestApp
{
    static class DbTest
    {
        public class TESTDB2 : DbProject
        {
            public TESTDB2(string connection_string) : base(connection_string)
            {
            }

            // テーブルの一覧
            public DbSet<Test> TestList { get; set; }

            // テーブルの定義
            [Table("test")]
            public class Test
            {
                [Key]
                public int test_id { get; set; }

                public DateTime test_dt { get; set; }
                [MaxLength(50)]
                public string test_str { get; set; }
            }
        }

        public static void db_test()
        {
            Cfg<DBTestSettings> cfg = new Cfg<DBTestSettings>();

            Con.WriteLine("a");
            using (TESTDB2 db = new TESTDB2(cfg.ConfigSafe.DBConnectStr))
            {
                Con.WriteLine("x");
                return;
                db.EnableConsoleDebug.Set(false);
                var test = db.TestList;

                foreach (var t in test)
                {
                    t.InnerDebug();
                }
            }
        }
    }
}

