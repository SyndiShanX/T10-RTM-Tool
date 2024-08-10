using System.Net.Sockets;
using System.Net;
using System.Windows;
using LibDebug;
using syndishanx_rtm_tool.ViewModels;
using System.IO;

namespace syndishanx_rtm_tool
{
    public partial class MainWindow : Window
    {
        public static Socket? _socket;

        public static Debugger? _ps4;

        public static Process? _gameProcess;

        private MainWindowViewModel _mainWindowViewModel = new();

        public bool fullBright = true;

        public KeyValuePair<string, string>? dvarList = new();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _mainWindowViewModel;

            Attach_Button.IsEnabled = false;
            Start_Lobby_Button.IsEnabled = false;
            Join_Party_Button.IsEnabled = false;
            Fix_Loadouts_Button.IsEnabled = false;
            Fullbright_Button.IsEnabled = false;
            Send_Command_Button.IsEnabled = false;
            Call_Dvar_Button.IsEnabled = false;
            Set_Map_Button.IsEnabled = false;
            Set_Gamemode_Button.IsEnabled = false;
        }

        private void Connect_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mainWindowViewModel.ConsoleIpAddress))
            {
                MessageBox.Show("Make sure you specify an IP address for your PS4.");
                return;
            }

            if (!File.Exists("ps4debug.bin"))
            {
                MessageBox.Show("Make sure you have the 'ps4debug.bin' file in the same directory as the executable.");
                return;
            }

            if (!IPAddress.TryParse(_mainWindowViewModel.ConsoleIpAddress, out var parsedConsoleIpAddress))
            {
                MessageBox.Show("Please specify a valid IP address for your PS4.");
                return;
            }

            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = 3000,
                    SendTimeout = 3000
                };

                _socket.Connect(new IPEndPoint(parsedConsoleIpAddress, 9090));

                _socket.SendFile("ps4debug.bin");

                _socket.Close();

                Attach_Button.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed injecting payload");
            }
        }

        private void Attach_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mainWindowViewModel.ConsoleIpAddress))
            {
                return;
            }

            try
            {
                _ps4 = new Debugger(_mainWindowViewModel.ConsoleIpAddress);

                _ps4.Connect();

                var processList = _ps4.GetProcessList();

                _gameProcess = processList.FindProcess("eboot.bin");

                if (_gameProcess is null)
                {
                    MessageBox.Show("Unable to find game process. Make sure the game is running.");
                }

                _ps4.Notify(222, "SyndiShanX's RTM Tool Loaded");

                Start_Lobby_Button.IsEnabled = true;
                Join_Party_Button.IsEnabled = true;
                Fix_Loadouts_Button.IsEnabled = true;
                Fullbright_Button.IsEnabled = true;
                Send_Command_Button.IsEnabled = true;
                Call_Dvar_Button.IsEnabled = true;
                Set_Map_Button.IsEnabled = true;
                Set_Gamemode_Button.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Start_Lobby_Button_Click(object sender, RoutedEventArgs e)
        {
            CBuf_AddText("xstartlobby");
            _ps4.Notify(222, "Creating Match...");
        }

        private void Join_Party_Button_Click(object sender, RoutedEventArgs e)
        {
            CBuf_AddText("xstartlobby;xpartygo");
        }

        private void Fix_Loadouts_Button_Click(object sender, RoutedEventArgs e)
        {
            CBuf_AddText("#x38badbb201f2e8588 1");
        }

        private void Fullbright_Button_Click(object sender, RoutedEventArgs e)
        {
            fullBright = !fullBright;

            var fullBrightText = fullBright ? "1" : "0";

            CallDvar("704554F429DAB488", fullBrightText);
            CallDvar("53D347C4D236E028", fullBrightText);
            CallDvar("AD42CA33A427DE58", fullBrightText);
            CallDvar("8667C0BB90C5BFC3", fullBrightText);
            CallDvar("DF200A089A3B3FEB", fullBrightText);

            if (fullBrightText == "1")
            {
                _ps4.Notify(222, "Fullbright Enabled");
            }
            else if (fullBrightText == "0")
            {
                _ps4.Notify(222, "Fullbright Disabled");
            }
        }

        static void CBuf_AddText(string text, bool notify = false)
        {
            if (_ps4 is null || _gameProcess is null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (text[text.Length - 1] != ';')
            {
                text += ";";
            }

            var length = text.Length;

            UIntPtr cmd_textArray = new UIntPtr(0x400000 + 0x4D6C350);

            int bufferSize = _ps4.ReadInt32(_gameProcess.pid, cmd_textArray + 0x10000);

            if (length > bufferSize)
            {
                MessageBox.Show("Overflow");
                return;
            }

            _ps4.WriteString(_gameProcess.pid, cmd_textArray, text);

            _ps4.WriteInt32(_gameProcess.pid, cmd_textArray + 0x10004, length);

            if (notify)
            {
                _ps4.Notify(222, text + " executed");
            }
        }

        private void Send_Command_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mainWindowViewModel.CommandBuffer))
            {
                return;
            }

            CBuf_AddText(_mainWindowViewModel.CommandBuffer);

            _ps4.Notify(222, "Ran Command: " + _mainWindowViewModel.CommandBuffer);
        }

        void CallDvar(string dvarHash, object? value = null)
        {
            if (value == null)
            {
                CBuf_AddText("#x3" + dvarHash, false);
            }
            else
            {
                CBuf_AddText("#x3" + dvarHash + " " + value, false);
            }
        }

        private void Call_Dvar_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mainWindowViewModel.SelectedDvar) || string.IsNullOrEmpty(_mainWindowViewModel.SelectedDvarValue))
            {
                return;
            }

            CallDvar(_mainWindowViewModel.SelectedDvar, _mainWindowViewModel.SelectedDvarValue);

            _ps4.Notify(222, "Ran Dvar: " + _mainWindowViewModel.SelectedDvar + " " + _mainWindowViewModel.SelectedDvarValue);
        }

        private void Set_Map_Button_Click(object sender, RoutedEventArgs e)
        {
            var namedMap = Combo_Box_Maps.Text;
            var internalMap = "";

            if (namedMap == "Protocol") {
                internalMap = "mp_t10_island";
            } else if (namedMap == "Skyline") {
                internalMap = "mp_t10_penthouse";
            } else if (namedMap == "Scud") {
                internalMap = "mp_t10_radar";
            } else if (namedMap == "Babylon") {
                internalMap = "mp_t10_sm_babylon";
            } else if (namedMap == "Gala") {
                internalMap = "mp_t10_sm_capital";
            } else if (namedMap == "Stakeout") {
                internalMap = "mp_t10_sm_flat";
            } else if (namedMap == "Pit") {
                internalMap = "mp_t10_sm_vorkuta_mine";
            } else if (namedMap == "Rewind") {
                internalMap = "mp_t10_stripmall";
            } else if (namedMap == "Derelict") {
                internalMap = "mp_t10_traingraveyard";
            }

            if (internalMap != "") {
                CBuf_AddText("#x3ef237da69bb64ef6 " + internalMap);
                _ps4.Notify(222, "Map Set to " + namedMap);
            }
        }

        private void Set_Gamemode_Button_Click(object sender, RoutedEventArgs e)
        {
            var namedMode = Combo_Box_Gamemodes.Text;
            var internalMode = "";

            if (namedMode == "Team Deathmatch")
            {
                internalMode = "war";
            }
            else if (namedMode == "Domination")
            {
                internalMode = "dom";
            }
            else if (namedMode == "Hardpoint")
            {
                internalMode = "koth";
            }
            else if (namedMode == "Gunfight")
            {
                internalMode = "arena";
            }
            else if (namedMode == "Prisoner Rescue")
            {
                internalMode = "rescue";
            }
            else if (namedMode == "Kill Order")
            {
                internalMode = "hvt";
            }
            else if (namedMode == "Bounty")
            {
                internalMode = "bounty";
            }
            else if (namedMode == "All or Nothing")
            {
                internalMode = "aon";
            }

            if (internalMode != "")
            {
                CBuf_AddText("set default_gametype_mp " + internalMode + ";xstartlobby;");
                _ps4.Notify(222, "Gamemode Set to " + namedMode);
            }
        }
    }
}