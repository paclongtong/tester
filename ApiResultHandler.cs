using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using friction_tester;

public static class ApiResultHandler
{
    private static readonly Dictionary<int, (string Message, Action AdditionalAction)> ErrorMap =
        new Dictionary<int, (string, Action)>
        {
            { 0, ("执行成功", null)},
            { 1, ("执行失败：检测命令执行条件是否满足", null) },
            { 2, ("版本不支持该API：请联系厂家", null) },
            { 7, ("参数错误：检测参数是否合理", null) },
            { -1, ("通讯失败：检查接线或更换板卡", null) },
            { -6, ("打开控制器失败：确认串口名或调用次数", null) },
            { -7, ("运动控制器无响应：检查连接或更换板卡", null) }
        };

    public static void HandleResult(int result)
    {
        if (ErrorMap.TryGetValue(result, out var info))
        {
            if (result != 0)
            {
                MessageBox.Show(info.Message);
                Logger.Log($"API return value error：{info.Message}，code：{result}");

            }
            info.AdditionalAction?.Invoke();

        }
        else
        {
            MessageBox.Show($"Unknown error：{result}");
            Logger.Log($"Unknown error：{result}");
        }
    }
}

