using FetchAreaname.Model;
using Microsoft.ApplicationBlocks.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FetchAreaname.Provider
{
    public class UserInfoProvider
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["UserDataConn"] == null ? "" :
            ConfigurationManager.ConnectionStrings["UserDataConn"].ToString();
        private readonly int batchSize = ConfigurationManager.AppSettings["BatchSize"].ToInt(200);
       
        public int BatchInsertUserInfo(List<UserInfo> list)
        {
            var count = 0;
            if (list == null || list.Count <= 0)
                return count;
            try
            {
                var strSql = new StringBuilder();
                foreach (var user in list)
                {
                    strSql.AppendFormat(@"
IF EXISTS (SELECT 1 FROM [UserData].[dbo].[UserInfo] WHERE [UserId] = {0})
BEGIN
  UPDATE [UserData].[dbo].[UserInfo] 
  SET 
 	[TrueName] = '{1}',
 	[MobilePhone] = '{2}',
 	[HomePhone] = '{3}',
 	[Email] = '{4}',
	[Address] = '{5}',
	[CityName] = '{6}',
	[RegionName] = '{7}',
	[ModityTime] = GETDATE()
  WHERE [UserId] = {0}
END
ELSE
BEGIN
  INSERT INTO [UserData].[dbo].[UserInfo]
           ([UserId]
           ,[TrueName]
           ,[MobilePhone]
           ,[HomePhone]
           ,[Email]
           ,[Address]
           ,[CityName]
           ,[RegionName]
           ,[CreatTime]
           ,[ModityTime])
     VALUES
           ({0}
           ,'{1}'
           ,'{2}'
           ,'{3}'
           ,'{4}'
           ,'{5}'
           ,'{6}'
           ,'{7}'
           ,GETDATE()
           ,GETDATE());
END;", user.UserId, user.TrueName, user.MobilePhone, user.HomePhone, user.Email, user.Address, user.CityName, user.RegionName);
                }
                count = SqlHelper.ExecuteNonQuery(connStr, CommandType.Text, strSql.ToString());
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return count;
        }

        public void BatchInsertUser(List<UserInfo> list)
        {
            var watch = Stopwatch.StartNew();
            var count = 0;
            while (list.Any())
            {
                try
                {
                    var thisList = list.Take(batchSize).ToList();
                    var result = new UserInfoProvider().BatchInsertUserInfo(thisList);
                    list.RemoveRange(0, result);
                    count += result;
                    Console.WriteLine(string.Format(@"{0} data insert/update success.", result));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format(@"insert/update fail,Exception:{0}", ex.Message));
                    break;
                }
            }
            watch.Stop();
            Console.WriteLine(string.Format(@"-- 批量插入/修改用户信息OVER -- 共{0}条数据 -- [{1} 秒]", count, Math.Round(watch.ElapsedMilliseconds / 1000d, 2)));
        }

        public int BatchInsertWord(List<string> words)
        {
            if (words == null || words.Count <= 0)
                return 0;

            var strSql = new StringBuilder();
            foreach (var info in words)
            {
                strSql.AppendFormat(@"
INSERT INTO[UserData].[dbo].[WordInfo]
           ([Word])
     VALUES
           ('{0}');
", info);
            }

            return SqlHelper.ExecuteNonQuery(connStr, CommandType.Text, strSql.ToString());
        }
    }

    public static class Extensions
    {
        public static int ToInt(this object obj, int defaultVal = 0)
        {
            var result = defaultVal;
            var objStr = obj == null ? "" : obj.ToString();
            int.TryParse(objStr, out result);
            return result <= 0 ? defaultVal : result;
        }
    }
}
