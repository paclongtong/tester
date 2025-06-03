using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class AxisConfig
{
    public int AxisId { get; set; }
    public double SoftLimitMin 
    { 
        get => _softLimitMin; 
        set
        {
            if ( double.TryParse(value.ToString().Trim(), out double parsedValue))
            {
                if (parsedValue < -1000000)
                {
                    throw new ArgumentOutOfRangeException("Soft limit min cannot be too small");
                }
                _softLimitMin = parsedValue;
            }
        }
    }
    private double _softLimitMin;
    public double SoftLimitMax
    {
            
        get => _softLimitMax;
        set
        {
                if (double.TryParse(value.ToString().Trim(), out double parsedValue))
                {
                    if (parsedValue < -1000000 || parsedValue > 3000000)
                    {
                        throw new ArgumentOutOfRangeException("Soft limit max cannot be negative or too large");
                    }
                    _softLimitMax = parsedValue;
                
                }
        }
    }
    private double _softLimitMax;
    public bool IsHardLimitEnabled { get; set; }
    public bool IsEStopEnabled { get; set; }
    public int HardLimitPositiveIO { get; set; }
    public int HardLimitNegativeIO { get; set; }

    // New: Speed when returning to origin
    private double _homeReturnSpeed = 50;
    public double HomeReturnSpeed
    {
        get => _homeReturnSpeed;
        set
        {
            //if (value < 1 || value > 10000)
            //    throw new ArgumentOutOfRangeException("Home return speed must be between 1 and 10000 mm/s");
            _homeReturnSpeed = value;
        }
    }
}
    
public class AppConfig
{
    public List<AxisConfig> Axes { get; set; } = new List<AxisConfig>();
    public bool GlobalEStopEnabled { get; set; }

    public string SelectedLanguage { get; set; } = "zh-CN";
}


