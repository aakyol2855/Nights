namespace Nights.Services.Interfaces;

public class SystemHealthStatsDto
{
    public string UptimeFormatted { get; set; } = "12g 7s";
    public int CpuPercent { get; set; } = 12;
    public int RamPercent { get; set; } = 34;
    public int DiskPercent { get; set; } = 62;
    public string ServerLabel { get; set; } = "istanbul-node";
    public bool IsOnline { get; set; } = true;

    // Mini sparkline grafikleri için son 6 veri noktası (0-100 arası)
    public int[] CpuHistory { get; set; } = [10, 18, 14, 22, 12, 16];
    public int[] RamHistory { get; set; } = [32, 33, 35, 34, 34, 34];
    public int[] DiskHistory { get; set; } = [62, 62, 62, 62, 62, 62];
}

public interface ISystemHealthService
{
    Task<SystemHealthStatsDto> GetHealthStatsAsync();
}
