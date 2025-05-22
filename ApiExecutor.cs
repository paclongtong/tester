using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using friction_tester;
public static class ApiExecutor
{
    public static int Execute(Func<int> apiCall)
    {
        try
        {
            int result = apiCall();
            ApiResultHandler.HandleResult(result);
            return result;
        }
        catch (Exception e)
        {
            //Logger.Log($"API 执行异常：{e.Message}");
            return -1;
        }
    }
    
    public static int Execute<T>(Func<T, int> apiCall, T arg)
    {
        int result = apiCall(arg);
        ApiResultHandler.HandleResult(result);
        return result;
    }
}

