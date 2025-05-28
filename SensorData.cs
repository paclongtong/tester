using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace friction_tester
{
    public class SensorData
    {
        //public int Id { get; set; } // Primary Key
        //public int TestId { get; set; } // Foreign Key to TestResult

        //public DateTime Timestamp { get; set; } = DateTime.Now;
        //public double Position { get; set; } // Position along the workpiece
        //public double SensorValue { get; set; } // e.g., Friction value
        //public JsonDocument AdditionalInfo { get; set; } // Optional JSON metadata

        //// Navigation Property
        //public TestResult TestResult { get; set; }


        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("test_id")]
        public int TestId { get; set; } // Foreign Key to TestResult

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        //[Column("position")]
        public double Position { get; set; }

        [Column("sensor_value")]
        public double SensorValue { get; set; }

        [Column("additional_info", TypeName = "jsonb")]
        public JsonDocument AdditionalInfo { get; set; }

        public TestResult TestResult { get; set; }
    }

    public class TimestampedSensorData
    {
        public double Position { get; set; }
        public double SensorValue { get; set; }
        public DateTime Timestamp { get; set; }
        public long MotionControllerTick { get; set; } // High-resolution timing from motion controller
        public bool IsValid { get; set; } = true;
        public string QualityFlag { get; set; } = "Good";
        public double PositionError { get; set; } // Difference between commanded and actual position
        public uint AxisStatus { get; set; } // Motion controller status
    }

}

