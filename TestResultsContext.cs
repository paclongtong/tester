using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;


namespace friction_tester
{
    public class TestResultsContext : DbContext
    {
        public DbSet<TestResult> TestResults { get; set; }
        public DbSet<SensorData> SensorData { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connString = "Host=localhost;Database=friction_tester;Username=postgres;Password=intin";
            Logger.Log($"DEBUG: Using connection string: {connString.Replace("Password=intin", "Password=****")}");
            optionsBuilder
                .UseNpgsql(connString)
                .EnableSensitiveDataLogging() // Add detailed SQL query logs
                .LogTo(Console.WriteLine);    // Log to console

            //try
            //{
            //    // If you use migrations:
            //    //this.Database.Migrate();
            //    // Or, for a quick-and-dirty “just create if missing”:
            //     this.Database.EnsureCreated();
            //}
            //catch (Exception ex)
            //{
            //    Logger.Log($"ERROR: Database initialization failed: {ex.Message}");
            //    throw;
            //}
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var jsonDocConverter = new ValueConverter<JsonDocument, string>(
                v => v.RootElement.GetRawText(), // Convert JsonDocument to JSON string when saving
                v => JsonDocument.Parse(v, new JsonDocumentOptions()) // Convert JSON string to JsonDocument when reading
            );

            modelBuilder.Entity<SensorData>(entity =>
            {
                entity.ToTable("sensor_data");
                entity.HasKey(sd => sd.Id);
                entity.Property(sd => sd.Id).HasColumnName("id");
                entity.Property(sd => sd.TestId).HasColumnName("test_id");
                entity.Property(sd => sd.Timestamp)
                      .HasColumnName("timestamp")
                      .HasColumnType("timestamp without time zone");
                entity.Property(sd => sd.Position).HasColumnName("position");
                entity.Property(sd => sd.SensorValue).HasColumnName("sensor_value");
                entity.Property(sd => sd.AdditionalInfo)
                      .HasColumnName("additional_info")
                      .HasColumnType("jsonb")
                      .HasConversion(jsonDocConverter);
            });

            modelBuilder.Entity<TestResult>(entity =>
            {
                entity.ToTable("test_results");
                entity.HasKey(tr => tr.Id);
                entity.Property(tr => tr.TimeStamp)
                      .HasColumnName("timestamp")
                      .HasColumnType("timestamp without time zone");
                entity.Property(tr => tr.TestName).HasColumnName("test_name");
                entity.Property(tr => tr.WorkpieceName).HasColumnName("workpiece_name");
                //entity.Property(tr => tr.OperatorName).HasColumnName("operator_name");
                entity.Property(tr => tr.Velocity).HasColumnName("velocity");
                entity.Property(tr => tr.Acceleration).HasColumnName("acceleration");
                entity.Property(tr => tr.StartPosition).HasColumnName("start_position");
                entity.Property(tr => tr.EndPosition).HasColumnName("end_position");
                entity.Property(tr => tr.TotalDuration).HasColumnName("total_duration");
                entity.Property(tr => tr.DistanceCovered).HasColumnName("distance_covered");
                entity.Property(tr => tr.AnomaliesDetected).HasColumnName("anomalies_detected");
                //entity.Property(tr => tr.AdditionalData)
                //      .HasColumnName("additional_data")
                //      .HasColumnType("jsonb");

                // Relationship configuration
                entity.HasMany(tr => tr.SensorData)
                      .WithOne(sd => sd.TestResult)
                      .HasForeignKey(sd => sd.TestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
    public class TestResult
    {
        [Key]
        [Column("id")] // Correctly map to "id"
        public int Id { get; set; }

        [Column("test_name")]
        public string TestName { get; set; }

        [Column("workpiece_name")]
        public string WorkpieceName { get; set; }

        //[Column("operator_name")]
        //public string OperatorName { get; set; }

        [Column("timestamp")]
        public DateTime TimeStamp { get; set; } = DateTime.Now;

        [Column("velocity")]
        public float Velocity { get; set; }

        [Column("acceleration")]
        public float Acceleration { get; set; }

        [Column("start_position")]
        public float StartPosition { get; set; }

        [Column("end_position")]
        public float EndPosition { get; set; }

        [Column("total_duration")]
        public double TotalDuration { get; set; }

        [Column("distance_covered")]
        public float DistanceCovered { get; set; }

        [Column("anomalies_detected")]
        public bool AnomaliesDetected { get; set; } = false;

        //[Column("additional_data", TypeName = "jsonb")]
        //public JsonDocument AdditionalData { get; set; }

        public List<friction_tester.SensorData> SensorData { get; set; } = new List<friction_tester.SensorData>(); // Navigation property
    }

    public class SensorData
    {
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

        //[Column("additional_info", TypeName = "jsonb")]
        //public JsonDocument AdditionalInfo { get; set; }

        public TestResult TestResult { get; set; }
}

