﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using iExchange.Common;
using AsyncSslServer.Session;
using System.Threading;
using System.Data;
using AsyncSslServer.Setting;
using AsyncSslServer.Bll;
using System.Xml;
using AsyncSslServer.Util;
using AsyncSslServer.TypeExtension;
using System.Configuration;
using System.IO;
using AsyncSslServer.Service;
namespace AsyncSslServer.Bll
{
    public class StatementService
    {
        private static TimeSpan _StatementReportTimeout = TimeSpan.MinValue;
        private static TimeSpan _LedgerReportTimeout = TimeSpan.MinValue;

        public static XmlNode LedgerForJava2(string session, string dateFrom, string dateTo, string IDs, string rdlc)
        {
            Guid result = Guid.Empty;
            try
            {
                AsyncResult asyncResult = new AsyncResult("LedgerForJava2", session);
                Token token = SessionManager.Default.GetToken(session);
                if (ThreadPool.QueueUserWorkItem(CreateLedger, new LedgerArgument(dateFrom, dateTo, IDs, rdlc, asyncResult, session)))
                {
                    result = asyncResult.Id;
                }
                else
                {
                    AppDebug.LogEvent("TradingConsole.LedgerForJava2:", "ThreadPool.QueueUserWorkItem failed", System.Diagnostics.EventLogEntryType.Warning);
                }
            }
            catch (System.Exception exception)
            {
                AppDebug.LogEvent("TradingConsole.LedgerForJava2:", exception.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }
            return XmlResultHelper.NewResult(result.ToString());
        }


        private static void CreateLedger(object state)
        {
            LedgerArgument ledgerArgument = (LedgerArgument)state;
            Token token = ledgerArgument.Token;
            string sql = "EXEC P_RptLedger @xmlAccounts=\'" + XmlTransform.Transform(ledgerArgument.IDs, ',', "Accounts", "Account", "ID") + "\',@tradeDayBegin=\'"
                + ledgerArgument.DateFrom + "\',@tradeDayTo=\'" + ledgerArgument.DateTo + "\',@language=\'" + ledgerArgument.Version + "\',@userID=\'" + ledgerArgument.Token.UserID.ToString() + "\'";
            try
            {
                DataSet dataSet = DataAccess.GetData(sql, SettingManager.Default.ConnectionString, LedgerReportTimeout);
                try
                {
                    TradingConsoleServer tradingConsoleServer = ledgerArgument.TradingConsoleServer;
                    tradingConsoleServer.SaveLedger(token, "",ledgerArgument.IDs);
                }
                catch
                {
                }

                if (dataSet.Tables.Count > 0)
                {
                    string filepath = Path.Combine(SettingManager.Default.PhysicPath, ledgerArgument.Rdlc); //this.Server.MapPath(ledgerArgument.Rdlc);
                    byte[] reportContent = PDFHelper.ExportPDF(filepath, dataSet.Tables[0]);
                    AsyncResultManager asyncResultManager = ledgerArgument.AsyncResultManager;
                    asyncResultManager.SetResult(ledgerArgument.AsyncResult, reportContent);
                    CommandManager.Default.AddCommand(ledgerArgument.Token, new AsyncCommand(0, ledgerArgument.AsyncResult));
                }
            }
            catch (System.Exception ex)
            {
                CommandManager.Default.AddCommand(ledgerArgument.Token, new AsyncCommand(0, ledgerArgument.AsyncResult, true, ex));
                AppDebug.LogEvent("TradingConsole.CreateLedger", sql + "\r\n" + ex.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }
        }




        public static XmlNode StatementForJava2(string session, int statementReportType, string dayBegin, string dayTo, string IDs, string rdlc)
        {
            Guid result = Guid.Empty;
            try
            {
                AsyncResult asyncResult = new AsyncResult("StatementForJava2", session);
                Token token = SessionManager.Default.GetToken(session);
                if (ThreadPool.QueueUserWorkItem(CreateStatement, new StatementArg(statementReportType, dayBegin, dayTo, IDs, rdlc, asyncResult, session)))
                {
                    result = asyncResult.Id;
                }
                else
                {
                    AppDebug.LogEvent("TradingConsole.StatementForJava2:", "ThreadPool.QueueUserWorkItem failed", System.Diagnostics.EventLogEntryType.Warning);
                }
            }
            catch (System.Exception exception)
            {
                AppDebug.LogEvent("TradingConsole.StatementForJava2:", exception.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }
            return XmlResultHelper.NewResult(result.ToString());

        }


        public static XmlNode GetReportContent(Guid asyncResultId)
        {
            String result = "";
            try
            {
                byte[] data = (byte[])Application.Default.AsyncResultManager.GetResult(asyncResultId);
                result = Convert.ToBase64String(data);
                Console.WriteLine("get report content");
                return XmlResultHelper.NewResult(result);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
                AppDebug.LogEvent("TradingConsole.GetReportContent", ex.ToString(), System.Diagnostics.EventLogEntryType.Error);
                return XmlResultHelper.ErrorResult;
            }
           
        }

        private static void CreateStatement(object state)
        {
            StatementArg statementArg = (StatementArg)state;
            Token token = statementArg.Token;
            string sql = string.Empty;
            if (statementArg.Rdlc.ToLower().Contains("statement_mc"))
            {
                sql = "EXEC P_RptStatement_RC2 ";
            }
            else
            {
                switch (statementArg.StatementReportType)
                {
                    case 0:
                        sql = "EXEC P_RptStatement_RC2 ";
                        break;
                    case 1:
                        sql = "EXEC P_RptStatement2_RC2 ";
                        break;
                    case 2:
                        sql = "EXEC P_RptStatement4_RC2 ";
                        break;
                    case 3:
                        sql = "EXEC P_RptStatement5_RC2 ";
                        break;
                }
            }
            sql += "@xmlAccounts=" + "\'" + XmlTransform.Transform(statementArg.IDs, ',', "Accounts", "Account", "ID")
                + "\',@tradeDayBegin=\'" + statementArg.DayBegin + "\',@tradeDayTo=\'" + statementArg.DayTo + "\',@language=\'" + statementArg.Version + "\',@userID=\'" + statementArg.Token.UserID.ToString() + "\'";
            try
            {
                DataSet dataSet = DataAccess.GetData(sql, SettingManager.Default.ConnectionString, StatementReportTimeout);
                try
                {
                    TradingConsoleServer tradingConsoleServer = statementArg.TradingConsoleServer;
                    tradingConsoleServer.SaveStatement(token, "", statementArg.IDs);
                }
                catch
                {
                }
                if (dataSet.Tables.Count > 0)
                {
                    string filepath = Path.Combine(SettingManager.Default.PhysicPath,statementArg.Rdlc); // this.Server.MapPath(statementArg.Rdlc);
                    Console.WriteLine(filepath);
                    if (statementArg.Rdlc.ToLower().Contains("statement_mc") && dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
                    {
                        if (!(bool)dataSet.Tables[0].Rows[0]["IsMultiCurrency"])
                        {
                            filepath = filepath.ToLower().Replace("rptStatement_mc.rdlc", "RptStatement.rdlc");
                        }
                    }
                    byte[] reportContent = PDFHelper.ExportPDF(filepath, dataSet.Tables[0]);
                    AsyncResultManager asyncResultManager = statementArg.AsyncResultManager;
                    asyncResultManager.SetResult(statementArg.AsyncResult, reportContent);
                    CommandManager.Default.AddCommand(statementArg.Token, new AsyncCommand(0, statementArg.AsyncResult));
                }
            }
            catch (System.Exception ex)
            {
                CommandManager.Default.AddCommand(statementArg.Token, new AsyncCommand(0, statementArg.AsyncResult, true, ex));
                AppDebug.LogEvent("TradingConsole.CreateStatement", sql + "\r\n" + ex.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private static TimeSpan StatementReportTimeout
        {
            get
            {
                if (_StatementReportTimeout == TimeSpan.MinValue)
                {
                    _StatementReportTimeout = TimeSpan.FromMilliseconds(int.Parse(ConfigurationManager.AppSettings["StatementReportTimeoutInMillsecond"]));
                }
                return _StatementReportTimeout;
            }
        }

        private static TimeSpan LedgerReportTimeout
        {
            get
            {
                if (_LedgerReportTimeout == TimeSpan.MinValue)
                {
                    _LedgerReportTimeout = TimeSpan.FromMilliseconds(int.Parse(ConfigurationManager.AppSettings["LedgerReportTimeoutInMillsecond"]));
                }
                return _LedgerReportTimeout;
            }
        }
    }
}