using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace friction_tester
{
    public class TestResultsRepository
    {
        private readonly TestResultsContext _context;

        public TestResultsRepository()
        {
            _context = new TestResultsContext();

            // check that we can actually talk to Postgres right away
            if (!_context.Database.CanConnect())
            {
                Logger.Log("ERROR: Cannot connect to PostgreSQL at localhost:5432");
                throw new InvalidOperationException("Database not reachable");
            }
        }

        public async Task AddTestResultAsync(TestResult testResult)
        {
            // verify connectivity before saving
            if (!await _context.Database.CanConnectAsync())
            {
                Logger.Log("ERROR: Cannot connect to DB before saving TestResult");
                throw new InvalidOperationException("Database not reachable");
            }

            try
            {
                await _context.TestResults.AddAsync(testResult);
                await _context.SaveChangesAsync();
                Logger.Log("Test result saved successfully.");
            }
            catch (Exception ex)
            {
                {
                    Logger.LogException(ex);
                    if (ex.InnerException != null)
                    {
                        MessageBox.Show($"Database Error: {ex.InnerException.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Logger.Log($"Inner Exception: {ex.InnerException.Message}");
                    }
                    else
                    {
                        MessageBox.Show($"An error occurred while saving test results: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        public async Task AddSensorDataAsync(List<SensorData> sensorDataList)
        {
            try
            {
                if (sensorDataList == null || !sensorDataList.Any())
                {
                    Logger.Log("Sensor data list is empty. No data to save.");
                    return;
                }

                if (!await _context.Database.CanConnectAsync())
                {
                    Logger.Log("ERROR: Cannot connect to DB before saving SensorData");
                    throw new InvalidOperationException("Database not reachable");
                }

                await _context.SensorData.AddRangeAsync(sensorDataList); // Add all entries at once
                await _context.SaveChangesAsync(); // Commit changes to the database

                Logger.Log($"Sensor data saved successfully. Total entries: {sensorDataList.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                if (ex.InnerException != null)
                {
                    MessageBox.Show($"Database Error: {ex.InnerException.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Logger.Log($"Inner Exception: {ex.InnerException.Message}");
                }
                else
                {
                    MessageBox.Show($"An error occurred while saving sensor data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async Task AddSensorDataAsync_Deprecated(List<SensorData> sensorDataList)
        {
            foreach (var sensorData in sensorDataList)
            {       
                try
                {
                    await _context.SensorData.AddAsync(sensorData);
                    await _context.SaveChangesAsync();
                    Logger.Log("Sensor data saved successfully.");
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    if (ex.InnerException != null)
                    {
                        MessageBox.Show($"Database Error: {ex.InnerException.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Logger.Log($"Inner Exception: {ex.InnerException.Message}");
                    }
                    else
                    {
                        MessageBox.Show($"An error occurred while saving sensor data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

        }


        public async Task<List<TestResult>> GetAllTestResultsAsync()
        {
            try
            {
                return await _context.TestResults.ToListAsync() ?? new List<TestResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show($"An error occurred while fetching test results: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogException(ex);
                return null;
            }
        }

        public async Task<TestResult> GetTestResultByIdAsync(int id)
        {
            //return await _context.TestResults.FindAsync(id);
            return await _context.TestResults
                .Include(tr => tr.SensorData)
                .FirstOrDefaultAsync(tr => tr.Id == id);
        }

        public async Task UpdateTestResultAsync(TestResult testResult)
        {
            _context.TestResults.Update(testResult);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTestResultAsync(int id)
        {
            var testResult = await _context.TestResults.FindAsync(id);
            if (testResult != null)
            {
                _context.TestResults.Remove(testResult);
                await _context.SaveChangesAsync();
            }
        }
    }
}
