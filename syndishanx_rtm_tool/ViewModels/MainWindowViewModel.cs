namespace syndishanx_rtm_tool.ViewModels
{
    class MainWindowViewModel
    {
        public string? CommandBuffer { get; set; } = "";
        public string? ConsoleIpAddress { get; set; } = "192.168.";
        public string? SelectedDvar { get; set; }

        public string? SelectedDvarValue { get; set; }

        public MainWindowViewModel() {}
    }
}
