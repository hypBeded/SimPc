using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SimPc.ClassLibrary
{
    public class DbHelper
    {
        private string connectionString;
        public DbHelper()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimPcDB.db");
            connectionString = $"Data Source={dbPath};Version=3;";
        }

           


    }
}
