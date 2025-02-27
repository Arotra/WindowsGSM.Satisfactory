using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsGSM.Plugins
{
    public class Satisfactory : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Satisfactory.Early", // WindowsGSM.XXXX
            author = "werewolf2150",
            description = "WindowsGSM plugin for supporting Satisfactory Dedicated Server Early Access",
            version = "1.3",
            url = "https://github.com/werewolf2150/WindowsGSM.Satisfactory", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1690800"; // Game server appId Steam

        // - Standard Constructor and properties
        public Satisfactory(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public override string StartPath => @"Engine\Binaries\Win64\UE4Server-Win64-Shipping.exe"; // Game server start path
        public string FullName = "Satisfactory Dedicated Server Early"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // TODO: Undisclosed method
        public object QueryMethod = null; // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "15777"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        public string BeaconPort = "15000"; // Default beacon port. This port currently cannot be set freely.
        
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name

        // TODO: May not support
        public string Maxplayers = "16"; // Default maxplayers

        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            UpdateServerCFG();
        }

        // - Update or create config file for max players param
        public void UpdateServerCFG()
        {
            // Check config directory
            string gameConfigDirectoryPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"FactoryGame\Saved\Config\WindowsServer");

            if (!Directory.Exists(gameConfigDirectoryPath))
            {
                Directory.CreateDirectory(gameConfigDirectoryPath);
            }

            // Create or update config file
            string gameConfigPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"FactoryGame\Saved\Config\WindowsServer\Game.ini");
            string gameConfigContentString = "[/Script/Engine.GameSession]\nMaxPlayers=" + _serverData.ServerMaxPlayer;
            byte[] info = new UTF8Encoding(true).GetBytes(gameConfigContentString);

            if (!File.Exists(gameConfigPath))
            {
                FileStream gameConfigFs = File.Create(gameConfigPath);
                gameConfigFs.Write(info, 0, info.Length);
            }
            else
            {
                string fileContent = File.ReadAllText(gameConfigPath);
                File.WriteAllText(gameConfigPath, Regex.Replace(fileContent, @"MaxPlayers=[0-9]*", $"MaxPlayers={_serverData.ServerMaxPlayer}"));
            }
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            UpdateServerCFG();

            // Prepare start parameter
            string param = "FactoryGame -log -unattended";
            param += $" {_serverData.ServerParam}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -Port={_serverData.ServerPort}"; 
            param += string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -ServerQueryPort={_serverData.ServerQueryPort}";
            //param += string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" -Multihome={_serverData.ServerIP}";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(20000);
            });
        }

        // fixes WinGSM bug, https://github.com/WindowsGSM/WindowsGSM/issues/57#issuecomment-983924499
        public async Task<Process> Update(bool validate = true, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: _serverData.ServerBeta, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

    }
}
