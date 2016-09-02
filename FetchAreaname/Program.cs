using FetchAreaname.Model;
using FetchAreaname.Provider;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FetchAreaname
{
    class Program
    {
        // 正则表达式
        const string EMAILPATTERN = @"Email = ((?!,)[\s\S])*,";
        const string TRUENAMEPATTERN = @"TrueName = ((?!,)[\s\S])*,";
        const string MOBILEPHONEPATTERN = @"MobilePhone = ((?!,)[\s\S])*,";
        const string HOMEPHONEPATTERN = @"HomePhone = ((?!,)[\s\S])*,";
        const string ADDRESSPATTERN = @"\[Address\] = ((?!WHERE)[\s\S])*WHERE";
        const string USERIDPATTERN = @" UserId = \d+";

        static readonly Dictionary<int, string> _addressWords = new Dictionary<int, string>(99999); // 地名字典,Key=HashCode,Value=AddressWord
        static readonly Dictionary<int, string> _cityNames = new Dictionary<int, string>(99999);    //城市   包括 市直辖区；区；县 等
        static readonly Dictionary<int, UserInfo> _userInfos = new Dictionary<int, UserInfo>(99999);// 用户信息,Key=HashCode,Value=UserInfo
        static readonly Queue<dynamic[]> _source = new Queue<dynamic[]>(99999);                     // 地址信息分词队列,[0]=AddressWord,[1]=UserInfo.HashCode

        static readonly Dictionary<int, List<string>> _addressWordUserInfos = new Dictionary<int, List<string>>(99999);
        

        static int _txtFileCount = 0;   // 用户信息文本文件的数量
        static List<bool> _isFinished;  // 用户信息文本文件分析结果，是否已完成，可通过 _isFinished.Count == _txtFileCount 来判断是否已分析完所有的文件
        static string appPath = AppDomain.CurrentDomain.BaseDirectory;

        static void Main(string[] args)
        {
            //读取小区（村）/城市关键字
            ReadRegionDict();

            var userInfoTxtPath = Path.Combine(appPath, "UserInfos");
            var userInfoTxtDirectoryInfo = Directory.Exists(userInfoTxtPath) ? new DirectoryInfo(userInfoTxtPath) : Directory.CreateDirectory(userInfoTxtPath);

            // TXT 目录
            var userInfoTxtFileInfos = userInfoTxtDirectoryInfo.GetFiles("*.txt");

            // 用于判断是否分析完地址信息
            _txtFileCount = userInfoTxtFileInfos.Length;
            _isFinished = new List<bool>(_txtFileCount);


            // 使用线程池异步分析 TXT 文件中的用户地址信息，地址分词写入队列 _source
            foreach (var userInfoTxtFileInfo in userInfoTxtFileInfos)
	        {
		        ThreadPool.QueueUserWorkItem(new WaitCallback(AnalysisUserInfoTxt), userInfoTxtFileInfo);
	        }

            // 开启线程异步分析分词队列中的地名
            StartExploreAddress();

            Console.ReadLine();
        }

        static void ReadRegionDict()
        {
             // 地名关键词目录
            var addressWordTxtPath = Path.Combine(appPath, "AddressWords");
            var addressWordTxtDirectoryInfo = Directory.Exists(addressWordTxtPath) ? new DirectoryInfo(addressWordTxtPath) : Directory.CreateDirectory(addressWordTxtPath);
            var addressWordTxtFileInfos = addressWordTxtDirectoryInfo.GetFiles("*.txt");

            Console.WriteLine("-------------读取小区（村）/城市关键字BEGIN---------------");
            // 读取地名关键词语
            foreach (var addressWordTxtFileInfo in addressWordTxtFileInfos)
            {
                var watch = Stopwatch.StartNew();
                var addrWordCount = 0;
                using (var fs = new FileStream(addressWordTxtFileInfo.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var sr = new StreamReader(fs, Encoding.Default))
                    {
                        while (!sr.EndOfStream)
                        {
                            var line = sr.ReadLine().Trim();
                            var hash = line.GetHashCode();

                            if (string.IsNullOrEmpty(line))
                                continue;

                            addrWordCount++;
                            if (!_addressWords.ContainsKey(hash))
                            {
                                _addressWords.Add(hash, line);
                            }
                        }
                    }
                }

                ReadCityDict();
                watch.Stop();
                Console.WriteLine(string.Format("{0} -- {1}个 -- [{2} 秒]", addressWordTxtFileInfo.Name, addrWordCount, Math.Round(watch.ElapsedMilliseconds / 1000d, 2)));
            }
            Console.WriteLine("--------------------读取小区（村）/城市关键字OVER-------------------");
            Console.WriteLine("------------------------读取用户信息（线程池）-----------------------");
            Console.ReadLine();
        }

        static void ReadCityDict()
        {
            //获取城市[市直；区；县]
            var cityNames = ConfigurationManager.AppSettings["CityNames"] == null ? "" : ConfigurationManager.AppSettings["CityNames"].ToString();
            if (!string.IsNullOrEmpty(cityNames))
            {
                var citys = cityNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (!citys.Any())
                    return;

                foreach (var city in citys)
                {
                    var hash = city.GetHashCode();
                    if (!_cityNames.ContainsKey(hash))
                    {
                        _cityNames.Add(hash, city);
                    }
                }
            }
        }

        static void AnalysisUserInfoTxt(object threadParameter)
        {
            var watch = Stopwatch.StartNew();

            var userInfoTxtFileInfo = (FileInfo)threadParameter;
            var count = 0;
            using (var fs = new FileStream(userInfoTxtFileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();

                        if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                            continue;

                        // 分析用户信息
                        var userInfo = MatchUserInfo(line);
                        var userInfoHashCode = userInfo.GetHashCode();

                        // 此处需加锁，否则多线程中操作Dictionary会出现混乱，造成用户信息缺失
                        lock (_userInfos)
                        {
                            if (!_userInfos.ContainsKey(userInfoHashCode))
                            {
                                _userInfos.Add(userInfoHashCode, userInfo);
                            }
                        }

                        // 对用户信息中的地址信息进行分词
                        MatchAddress(userInfo.Address, userInfoHashCode);
                        count++;
                    }
                }
            }

            // 分析完毕
            lock (_isFinished)
            {
                _isFinished.Add(true);
            }

            watch.Stop();
            Console.WriteLine(string.Format("{0} -- {1}条数据 -- [{2} 秒]", userInfoTxtFileInfo.Name, count, Math.Round(watch.ElapsedMilliseconds / 1000d, 2)));
        }

        static UserInfo MatchUserInfo(string line)
        {
            var patterns = new Dictionary<string, dynamic[]>(5);
            patterns.Add(EMAILPATTERN, new dynamic[3] { "Email", 8, 2 });
            patterns.Add(TRUENAMEPATTERN, new dynamic[3] { "TrueName", 13, 3 });
            patterns.Add(MOBILEPHONEPATTERN, new dynamic[3] { "MobilePhone", 16, 3 });
            patterns.Add(HOMEPHONEPATTERN, new dynamic[3] { "HomePhone", 14, 3 });
            patterns.Add(ADDRESSPATTERN, new dynamic[3] { "Address", 14, 6 });
            patterns.Add(USERIDPATTERN, new dynamic[3] { "UserId", 10, 0 });

            var userInfo = new UserInfo();

            foreach (var kv in patterns)
            {
                var pattern = kv.Key;
                var propertyName = (string)kv.Value[0];
                var propertyInfo = typeof(UserInfo).GetProperty(propertyName);
                var startIndex = (int)kv.Value[1];   // 开始索引位置,用于截取字符串
                var endIndex = (int)kv.Value[2];     // 结束索引长度,用于截取字符串
                var regex = new Regex(pattern);
                var matches = regex.Matches(line);
                foreach (Match match in matches)
                {
                    if (!match.Success) continue;
                    var matchValue = match.Value;
                    var value = matchValue.Substring(startIndex, matchValue.Length - endIndex - startIndex).Trim();
                    propertyInfo.SetValue(userInfo, value);
                }
            }

            return userInfo;
        }

        static void MatchAddress(string address, int userInfoHashCode)
        {
            const int AddressMinLen = 2;
            const int AddressMaxLen = 12;

            var addressArray = address.ToArray();
            var tempQueue = new Queue<dynamic[]>(99);

            for (int i = AddressMinLen; i <= AddressMaxLen; i++)
            {
                //北京市石景山区鲁谷路35号冠辉大厦4层（2个字 循环字符串次数为length-1）
                for(int j = 0;j < addressArray.Length - i + 1;j++)
                {
                    //从第j个位置开始截取 i个长度的字符串
                    //var addressWord = address.Substring(j, i);
                    var wordChars = new char[i];
                    for (int k = j; k < i + j; k++)
                    {
                        wordChars[k - j] = addressArray[k];
                    }
                    var addressWord = new string(wordChars);
                        //for (int j = i - 1; j < addressArray.Length; j++)
                        //{

                        //var current = addressArray[j];
                        //var wordChars = new char[i];



                        //for (int k = i - 1; k > 0; k--)
                        //{
                        //    wordChars[i - k - 1] = addressArray[j - k];
                        //}

                        //wordChars[i - 1] = current;

                        //var addressWord = new string(wordChars);

                        // 将地址分词跟用户信息对应起来
                        tempQueue.Enqueue(new dynamic[2] { addressWord, userInfoHashCode });
                }
            }

            // 一次性入队：比在循环中锁定入队效率要高
            lock (_source)
            {
                while (tempQueue.Count > 0)
                {
                    var temp = tempQueue.Dequeue();
                    _source.Enqueue(temp);
                }
            }
        }

        static void StartExploreAddress()
        {
            Console.WriteLine("-----------------匹配用户地址和小区（村）/城市BEGIN-----------------");
            Console.ReadLine();
            var watch = Stopwatch.StartNew();

            Action threadAction = () =>
            {
                while (true)
                {
                    var @continue = _source.Count > 0;
                    if (!@continue)
                    {
                        // 结束
                        if (_isFinished.Count == _txtFileCount)
                        {
                            Console.WriteLine(string.Format("{0:4}\t{1:10}\t{2:20}\t{3}", "UserId", "CityName", "RegionName", "Address"));

                            // 输出地名及地名出现的次数结果
                            foreach (var kv in _addressWordUserInfos)
                            {
                                var addrInfos = kv.Value;
                                var cityName = "";
                                if (addrInfos.Any())//取城市
                                {
                                    cityName = addrInfos[0];
                                    addrInfos.RemoveAt(0);
                                }
                                // 此处同一个用户会存在多个小区（村）名称，例如：大刘|大刘村 使用长度最大的小区（村）名称 
                                var regionName = addrInfos.OrderByDescending(a => a.Length).FirstOrDefault();

                                _userInfos[kv.Key].CityName = cityName;
                                _userInfos[kv.Key].RegionName = regionName;
                                Console.WriteLine(string.Format("{0:4}\t{1:10}\t{2:20}\t{3}", _userInfos[kv.Key].UserId, cityName, regionName, _userInfos[kv.Key].Address));
                            }
                            watch.Stop();
                            Console.WriteLine(string.Format("-----------------匹配用户地址和小区（村）/城市OVER---[{0} 秒]-----------", Math.Round(watch.ElapsedMilliseconds / 1000d, 2)));

                            var userList = _userInfos.Select(c => c.Value).ToList();
                            if (userList.Any())
                            {
                                Console.WriteLine(@"-----------------批量插入/修改用户信息BEGIN-----------------");
                                new UserInfoProvider().BatchInsertUser(userList);
                            }
                            break;
                        }

                        Thread.Sleep(1);
                        continue;
                    }

                    dynamic[] infos = new dynamic[2] { string.Empty, default(int) };

                    lock (_source)
                    {
                        infos = _source.Dequeue();
                    }

                    if (infos == null)
                        continue;

                    var addressWord = (string)infos[0];
                    var addressWordHashCode = addressWord.GetHashCode();
                    var userInfoHashCode = (int)infos[1];
                    //出队列分词信息匹配城市信息
                    var matchResult = new List<bool>() { _cityNames.ContainsKey(addressWordHashCode) };
                    if (!matchResult[0])//未匹配到城市，则继续匹配是否为小区（村）
                        matchResult.Add(_addressWords.ContainsKey(addressWordHashCode));
                    else
                        matchResult.Add(false);

                    // 匹配到了城市或者小区（村），则将其和用户信息关联。。为城市，则将其信息放入list[0]中【没匹配到，则空着；防止城市和小区（村）信息错乱分不开】，后面list[1]...之中放入的都是小区信息（小区（村）信息可能匹配到多个）
                    // 此处为同一个用户匹配到的多个小区（村）分词名称
                    // 例如：大刘 = 大刘村
                    // 有两种解决方法
                    // 方法1：从地名字典中，去掉小区（村）简写名称
                    // 方法2：在此处选择最长的小区（村）名称，忽略小区（村）简写的名称
                    if (matchResult[0] || matchResult[1])
                    {
                        //不包含Key，则初始化匹配到的城市或小区（村）List信息；且把list[0]添加进来预留给cityName用。 
                        if (!_addressWordUserInfos.ContainsKey(userInfoHashCode))
                        {
                            _addressWordUserInfos.Add(userInfoHashCode, new List<string>() { "" });
                        }

                        //为小区（村），则往list的后面添加；为城市，将cityName放入list[0]
                        if (matchResult[1])
                            _addressWordUserInfos[userInfoHashCode].Add(addressWord);
                        else
                            _addressWordUserInfos[userInfoHashCode][0] = addressWord;
                    }
                }
            };

            var thread = new Thread(new ThreadStart(threadAction));
            thread.IsBackground = false;
            thread.Start();
        }
    }

   
}
