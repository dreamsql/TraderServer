﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using iExchange.Common;
using System.Xml;
using Trader.Server.Session;
using System.Diagnostics;
using System.Threading;
using Trader.Server.Setting;
using System.Collections;
using System.Data;
using Trader.Server.Bll;
using Trader.Server.Util;
using Trader.Server.TypeExtension;
using Trader.Helper;
using Trader.Common;
using log4net;
using System.Xml.Linq;
using System.Collections.Concurrent;
namespace Trader.Server.Service
{
    public class CommandManager
    {
        private CommandQueue _Commands;
        private ILog _Logger = LogManager.GetLogger(typeof(CommandManager));
        private AutoResetEvent _Event = new AutoResetEvent(false);
        private ConcurrentQueue<Command> _Queue = new ConcurrentQueue<Command>();
        private volatile bool _IsStop = false;
        private volatile bool _IsStart = false;
        private Command _Current;

        private CommandManager()
        {
            this._Commands= new CommandQueue(new TimeSpan(0, 20, 0));
          
        }
        public static readonly CommandManager Default = new CommandManager();

        public void Start()
        {
            if (this._IsStart)
            {
                return;
            }
            Thread thread = new Thread(ProcessCommand);
            thread.IsBackground = true;
            thread.Start();
            this._IsStart = true;
        }

        public void Stop()
        {
            this._IsStop = true;
        }

        public void Send(Command command)
        {
            this._Queue.Enqueue(command);
            this._Event.Set();
        }

        private void ProcessCommand()
        {
            while (true)
            {
                if (this._IsStop)
                {
                    break;
                }
                this._Event.WaitOne();
                while (this._Queue.TryDequeue(out this._Current))
                {
                    QuotationCommand quotation = this._Current as QuotationCommand;
                    if (quotation != null)
                    {
                        QuotationDispatcher.Default.Add(quotation);
                    }
                    else
                    {
                        CompositeCommand compositeCommand = this._Current as CompositeCommand;
                        if (compositeCommand != null)
                        {
                            foreach (var cmd in compositeCommand.Commands)
                            {
                                this._Commands.Add(cmd);
                                var target = CommandWithQuotationPool.Default.Pop();
                                target.Initialize(null, cmd, false);
                                AgentController.Default.AddQuotation(target);
                            }
                        }
                        else
                        {
                            this._Commands.Add(this._Current);
                            var target = CommandWithQuotationPool.Default.Pop();
                            target.Initialize(null, this._Current, false);
                            AgentController.Default.AddQuotation(target);
                        }
                    }
                }
            }
        }



        public int LastSequence
        {
            get
            {
                return this._Commands.LastSequence;
            }
        }

        public void AddCommand(Command command)
        {
            this.Send(command);
        }


        public void AddQuotation(QuotationCommand quotation)
        {
            this._Commands.Add(quotation);
            var target = CommandWithQuotationPool.Default.Pop();
            target.Initialize(quotation, null, true);
            AgentController.Default.AddQuotation(target);
        }


        public XmlNode VerifyRefrence(long session,State state, XmlNode xmlCommands, out bool changed)
        {
            changed = false;
            if (!(state is TradingConsoleState)) return xmlCommands;

            TradingConsoleState tradingConsoleState = (TradingConsoleState)state;

            ArrayList instrumentIDs = new ArrayList();

            try
            {
                foreach (XmlElement xmlElement in xmlCommands.ChildNodes)
                {
                    XmlElement tran = null;
                    Guid instrumentID = Guid.Empty;
                    switch (xmlElement.Name)
                    {
                        case "Execute2":
                            tran = (XmlElement)xmlElement.GetElementsByTagName("Transaction")[0];
                            instrumentID = XmlConvert.ToGuid(tran.Attributes["InstrumentID"].Value);
                            if (!tradingConsoleState.Instruments.ContainsKey(instrumentID))
                            {
                                instrumentIDs.Add(instrumentID);
                                changed = true;
                            }
                            break;
                        case "Place":
                            tran = (XmlElement)xmlElement.GetElementsByTagName("Transaction")[0];
                            instrumentID = XmlConvert.ToGuid(tran.Attributes["InstrumentID"].Value);
                            if (!tradingConsoleState.Instruments.ContainsKey(instrumentID))
                            {
                                instrumentIDs.Add(instrumentID);
                                changed = true;
                            }
                            break;
                    }
                }

                if (instrumentIDs.Count > 0)
                {
                    DataSet dataSet = this.GetInstruments(session,instrumentIDs);
                    XmlDocument xmlDocument = xmlCommands.OwnerDocument;
                    XmlElement parameters = xmlDocument.CreateElement("Parameters");

                    System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
                    System.IO.StringWriter instrumentWriter = new System.IO.StringWriter(stringBuilder);
                    System.Xml.XmlWriter instrumentXmlWriter = new XmlTextWriter(instrumentWriter);

                    System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(DataSet));
                    xmlSerializer.Serialize(instrumentXmlWriter, dataSet);

                    parameters.SetAttribute("DataSet", stringBuilder.ToString());
                    xmlCommands.InsertBefore(parameters, xmlCommands.FirstChild);
                    changed = true;
                }
            }
            catch (System.Exception ex)
            {
                AppDebug.LogEvent("TradingConsole.VerifyRefrence", ex.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }

            return xmlCommands;
        }
        private XmlNode VerifyRefrence(long session,State state, XmlNode xmlCommands)
        {
            bool changed;
            return this.VerifyRefrence(session,state, xmlCommands, out changed);
        }

        public DataSet GetInstruments(long session,ArrayList instrumentIDs)
        {
            try
            {
                TradingConsoleState state = SessionManager.Default.GetTradingConsoleState(session);
                Token token = SessionManager.Default.GetToken(session);
                DataSet dataSet = Application.Default.TradingConsoleServer.GetInstruments(token, instrumentIDs, state);
                return dataSet;
            }
            catch (System.Exception exception)
            {
                AppDebug.LogEvent("TradingConsole.GetInstruments:", exception.ToString(), System.Diagnostics.EventLogEntryType.Error);
            }
            return null;
        }


        public XElement GetLostCommands(long session,int firstSequence, int lastSequence)
        {
            XmlNode xmlCommands = null;
            try
            {
                State state = SessionManager.Default.GetTradingConsoleState(session);
                Token token = SessionManager.Default.GetToken(session);
                if (Command.CompareSequence(firstSequence, lastSequence) > 0 )
                {
                    AppDebug.LogEvent("TradingConsole.GetCommands2:", string.Format("{0},range == [{1},{2}]", token,  firstSequence, lastSequence), EventLogEntryType.Warning);
                    return null;
                }
                xmlCommands = this._Commands.GetCommands(token, state, firstSequence, lastSequence);
                xmlCommands = this.VerifyRefrence(session, state, xmlCommands);
                AppDebug.LogEvent("TradingConsole.GetCommands2:", string.Format("{0},  range == [{1},{2}]\n{3}", token,  firstSequence, lastSequence, xmlCommands.OuterXml), EventLogEntryType.Warning);
                return XmlResultHelper.NewResult(xmlCommands.OuterXml);
            }
            catch (System.Exception ex)
            {
                AppDebug.LogEvent("TradingConsole.GetCommands2", ex.ToString(), System.Diagnostics.EventLogEntryType.Error);
                return XmlResultHelper.ErrorResult;
            }
           
        }

        
    }

    public class CommandWithQuotation
    {
        private QuotationCommand _QuotationCommand;
        public QuotationCommand QuotationCommand { get { return this._QuotationCommand; } }
        private Command _Command;
        public Command Command { get { return this._Command; } }
        private bool _isQuotation;
        public bool IsQuotation { get { return this._isQuotation; } }
        public void Initialize(QuotationCommand quotation, Command command, bool isQuotation)
        {
            this._QuotationCommand = quotation;
            this._Command = command;
            this._isQuotation = isQuotation;
        }

    }
}
