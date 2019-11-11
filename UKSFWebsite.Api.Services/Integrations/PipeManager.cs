using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Services.Integrations.Procedures;

namespace UKSFWebsite.Api.Services.Integrations {
    public class PipeManager : IPipeManager {
        private const string PIPE_COMMAND_CLOSE = "1";
        private const string PIPE_COMMAND_OPEN = "0";
        private const string PIPE_COMMAND_READ = "3";
        private const string PIPE_COMMAND_RESET = "4";
        private const string PIPE_COMMAND_WRITE = "2";
        private static DateTime connectionCheckTime = DateTime.Now;
        private static DateTime pingCheckTime = DateTime.Now;
        public static DateTime PongTime = DateTime.Now;
        private readonly List<ITeamspeakProcedure> procedures;
        private int pipeCode;

        private bool runAll, runServer, serverStarted;

        public PipeManager(Pong pong, CheckClientServerGroup checkClientServerGroup, SendClientsUpdate sendClientsUpdate) => procedures = new List<ITeamspeakProcedure> {pong, checkClientServerGroup, sendClientsUpdate};

        public void Dispose() {
            runAll = false;
            runServer = false;
        }

        public void Start() {
            runAll = true;
            runServer = true;
            Task.Run(
                async () => {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    ConnectionCheck();
                    while (runAll) {
                        try {
                            if (runServer && (DateTime.Now - connectionCheckTime).Seconds > 1) {
                                connectionCheckTime = DateTime.Now;
                                ConnectionCheck();
                            }

                            if (serverStarted && runServer && (DateTime.Now - pingCheckTime).Seconds > 2) {
                                pingCheckTime = DateTime.Now;
                                if (!PingCheck()) {
                                    runServer = false;
                                    Task unused = Task.Run(
                                        async () => {
                                            await Task.Delay(TimeSpan.FromSeconds(2));

                                            pipeCode = 0;
                                            runServer = true;
                                            PongTime = DateTime.Now;
                                        }
                                    );
                                }
                            }

                            ReadCheck();
                            WriteCheck();
                            await Task.Delay(TimeSpan.FromMilliseconds(1));
                        } catch (Exception exception) {
                            Console.WriteLine(exception);
                        }
                    }
                }
            );
        }

        [DllImport("serverpipe.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.BStr)]
        private static extern string ExecutePipeFunction([MarshalAs(UnmanagedType.BStr)] string args);

        private void ConnectionCheck() {
            if (pipeCode != 1) {
                try {
                    PongTime = DateTime.Now;
                    Console.WriteLine("Opening pipe");
                    string result = ExecutePipeFunction(PIPE_COMMAND_OPEN);
                    Console.WriteLine(result);
                    int.TryParse(result, out pipeCode);
                    serverStarted = pipeCode == 1;
                } catch (Exception exception) {
                    Console.WriteLine(exception);
                    pipeCode = 0;
                    serverStarted = false;
                }
            }
        }

        private static bool PingCheck() {
            ExecutePipeFunction($"{PIPE_COMMAND_WRITE}{ProcedureDefinitons.PROC_PING}:");
            if ((DateTime.Now - PongTime).Seconds > 10) {
                Console.WriteLine("Resetting pipe");
                string result = ExecutePipeFunction(PIPE_COMMAND_RESET);
                Console.WriteLine(result);
                return false;
            }

            return true;
        }

        private void ReadCheck() {
            if (pipeCode != 1) return;
            string result = ExecutePipeFunction(PIPE_COMMAND_READ);
            if (string.IsNullOrEmpty(result)) return;
            switch (result) {
                case "FALSE":
                    Console.WriteLine("Closing pipe");
                    result = ExecutePipeFunction(PIPE_COMMAND_CLOSE);
                    Console.WriteLine(result);
                    pipeCode = 0;
                    return;
                case "NULL":
                case "NOT_CONNECTED": return;
                default:
                    HandleMessage(result);
                    break;
            }
        }

        private void HandleMessage(string message) {
            string[] parts = message.Split(new[] {':'}, 2);
            string procedureName = parts[0];
            if (string.IsNullOrEmpty(procedureName)) return;

            if (parts.Length > 1) {
                ITeamspeakProcedure procedure = procedures.FirstOrDefault(x => x.GetType().Name == procedureName);
                if (procedure != null) {
                    string[] args = parts[1].Split('|');
                    Task.Run(() => procedure.Run(args));
                }
            }
        }

        private void WriteCheck() {
            if (pipeCode != 1) return;
            string message = PipeQueueManager.GetMessage();
            if (string.IsNullOrEmpty(message)) return;
            string result = ExecutePipeFunction($"{PIPE_COMMAND_WRITE}{message}");
            if (result != "WRITE") {
                Console.WriteLine(result);
            }
        }
    }
}
