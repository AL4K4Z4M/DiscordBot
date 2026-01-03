using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Scrappy.Services;

public class SystemMonitorService
{
    private readonly Process _currentProcess;

    public SystemMonitorService()
    {
        _currentProcess = Process.GetCurrentProcess();
    }

    public double GetCpuUsage()
    {
        // Simple approximation for process CPU usage
        var startTime = DateTime.UtcNow;
        var startCpuUsage = _currentProcess.TotalProcessorTime;
        
        Thread.Sleep(100);
        
        var endTime = DateTime.UtcNow;
        var endCpuUsage = _currentProcess.TotalProcessorTime;
        
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        
        return cpuUsageTotal * 100;
    }

    public long GetMemoryUsage() => _currentProcess.WorkingSet64;

    public dynamic GetStorageInfo()
    {
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith(Path.GetPathRoot(Environment.CurrentDirectory)!)) 
                    ?? DriveInfo.GetDrives().First(d => d.IsReady);

        return new
        {
            DriveName = drive.Name,
            TotalSize = drive.TotalSize,
            AvailableFreeSpace = drive.AvailableFreeSpace,
            UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
            UsagePercentage = Math.Round((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100, 2)
        };
    }
}
