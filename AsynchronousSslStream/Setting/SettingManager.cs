﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuaInterface;
using log4net;
using AsyncSslServer.TypeExtension;
using System.Configuration;
namespace AsyncSslServer.Setting
{
    public class SettingManager
    {
        private ILog _Logger = LogManager.GetLogger(typeof(SettingManager));
        private Lua _LuaVM = new Lua();
        private Dictionary<string,string> _LoginSettings;
        private SettingManager()
        {
            try
            {
                var fun = _LuaVM.LoadFile(@"Setting/Script/setting.lua");
                fun.Call();
                this.PhysicPath = GetSettingFromAppSettingConfig("physicPath");
                this.ConnectionString = GetSettingFromAppSettingConfig("connectionString");
                this.ConnectionStringForReport = GetSettingFromAppSettingConfig("connectionStringForReport");
                this._LoginSettings = _LuaVM.GetTable("settings").ToDict();
                this.BackofficeServiceUrl = GetSettingFromAppSettingConfig("backofficeServiceUrl");
                this.IsDebugGetCommands = Boolean.Parse(_LuaVM.GetString("isDebugGetCommands"));
                this.ServerPort = int.Parse(GetSettingFromAppSettingConfig("serverPort"));
                this.SessionExpiredTimeSpan = new TimeSpan(0, int.Parse(GetSettingFromAppSettingConfig("SessionExpiredTimeSpan")), 0);
                this.CommandUrl = GetSettingFromAppSettingConfig("commandUrl");
                this.CertificatePath = GetSettingFromAppSettingConfig("CertificatePath");
            }
            catch (Exception ex)
            {
                _Logger.ErrorFormat("Load setting failed: {0}", ex);
                Console.WriteLine(ex);

            }
        }
        public static readonly SettingManager Default = new SettingManager();

        public int ServerPort { get; private set; }

        public string PhysicPath { get; private set; }
        public string BackofficeServiceUrl { get; private set; }

        public string ConnectionString { get; private set; }

        public string ConnectionStringForReport { get; private set; }

        public bool IsDebugGetCommands { get; private set; }
        public TimeSpan SessionExpiredTimeSpan { get; set; }
        public string CommandUrl { get; private set; }
        public string CertificatePath { get; private set; }

        public string GetLoginSetting(string key)
        {
            if (this._LoginSettings.ContainsKey(key))
            {
                return this._LoginSettings[key];
            }
            return string.Empty;
        }

        private string GetSettingFromAppSettingConfig(string key)
        {
            try
            {
                return ConfigurationManager.AppSettings[key];
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("{0} can't be found",key), ex);
            }
        }




    }
}