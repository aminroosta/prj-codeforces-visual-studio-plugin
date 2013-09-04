using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace codeforces_vs_addin.test
{
    
    class Program
    {
        public static Database db = Database.Instance;
        //[STAThread]
        static void Main(string[] args)
        {
            Task.Run(() =>
            {
                WebBrowser wb = new WebBrowser();
                wb.Navigate("http://codeforces.com/enter");
                wb.DocumentCompleted += (sender, e) =>
                {
                    HtmlElementCollection col = wb.Document.GetElementsByTagName("input");
                    for (int i = 0; i < col.Count; ++i)
                        if (col[i].Name == "handle")
                            col[i].InnerText = "000golabi";
                        else if (col[i].Name == "password")
                            col[i].InnerText = "pas";
                    Form form = new Form();
                    wb.Parent = form;
                    form.Show();
                };
            });

            Console.ReadLine();
            
        }

        public static CookieCollection first_request(out string crlf)
        {
            CookieCollection cookies = new CookieCollection();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://codeforces.com/enter");
            request.Init();
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            cookies = response.Cookies;

            crlf = string.Empty;
            using (var stream = response.GetResponseStream())
            {

                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                crlf = reader.ReadToEnd();
                crlf = crlf.ParseHtml(@"<meta name=""X-Csrf-Token"" content=""", @"""/>").FirstOrDefault();
            }
            return cookies;
        }
        public static CookieCollection login_request(string crlf, string username, string password,CookieCollection cookies)
        {
            string getUrl = "http://codeforces.com/enter";
            string postData = String.Format("csrf_token={0}&action=enter&handle={1}&password={2}&_tta=740", crlf, username, password);
            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(getUrl);
            getRequest.CookieContainer = new CookieContainer();
            getRequest.CookieContainer.Add(cookies); //recover cookies First request
            getRequest.Method = WebRequestMethods.Http.Post;
            getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
            getRequest.AllowWriteStreamBuffering = true;
            getRequest.ProtocolVersion = HttpVersion.Version11;
            getRequest.AllowAutoRedirect = true;
            getRequest.ContentType = "application/x-www-form-urlencoded";

            byte[] byteArray = Encoding.ASCII.GetBytes(postData);
            getRequest.ContentLength = byteArray.Length;
            Stream newStream = getRequest.GetRequestStream(); //open connection
            newStream.Write(byteArray, 0, byteArray.Length); // Send the data.
            newStream.Close();

            HttpWebResponse getResponse = (HttpWebResponse)getRequest.GetResponse();
            cookies = getResponse.Cookies;
            return cookies;
        }
        public static string get_request(string url,ref CookieCollection cookies)
        {
            WebClientEx wc = new WebClientEx();
            wc._cookieContainer.Add(cookies);
            wc.Init();
            return wc.DownloadString(url);


            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Init();
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            cookies = response.Cookies;

            string page = string.Empty;
            using (var stream = response.GetResponseStream())
            {

                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                page = reader.ReadToEnd();
            }
            return page;
        }
    }

    public class WebClientEx : WebClient
    {
        public CookieContainer _cookieContainer = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
            {
                (request as HttpWebRequest).CookieContainer = _cookieContainer;
            }
            return request;
        }
    }


    public class WebReader
    {
        public static List<Contest> ReadContestsAll(string url)
        {
            List<Contest> contests = new List<Contest>();
            string page = string.Empty;
            List<string> fractions = null;

            using (WebClient wc = new WebClient())
            {
                wc.Init();
                page = wc.DownloadString(url);
            }
            string pattern = @"data-contestId=""";
            for (int index = 0; index < page.Length; index += pattern.Length)
            {
                index = page.IndexOf(pattern, index);
                if (index == -1)
                    break;
                index += pattern.Length;
                Contest c = new Contest();
                c.Url = "http://codeforces.com/contest/" + page.Substring(index, page.IndexOf(@"""", index) - index);
                index = page.IndexOf(@"<td>", index) + @"<td>".Length;
                string cname = page.Substring(index, page.IndexOf(@"</td>", index) - index);
                if (cname.Contains(@"<br/>"))
                {
                    c.Available = true;
                    cname = cname.Substring(0, cname.IndexOf(@"<br/>"));
                }
                c.Name = cname.Trim();
                contests.Add(c);
            }
            return contests;
        }

        public static List<Problem> ReadProblemsAll(Contest contest)
        {
            List<Problem> problems = new List<Problem>();
            string page = string.Empty;
            using (WebClient wc = new WebClient())
            {
                wc.Init();
                Console.WriteLine(contest.Url + "/problems");
                page = wc.DownloadString(contest.Url + "/problems");
            }
            List<string> titles = page.ParseHtml(@"<div class=""title"">", @"</div>");
            List<string> inputs = page.ParseHtml(@"<div class=""title"">Input</div><pre>", "</pre>");
            List<string> outputs = page.ParseHtml(@"<div class=""title"">Output</div><pre>", "</pre>");

            for (int i = 0, j = 0; i < titles.Count && j < inputs.Count; )
            {
                Problem problem = new Problem();
                problem.Name = contest.Name + " " + titles[i];
                while (++i < titles.Count && (titles[i] == "Input" || titles[i] == "Output"))
                    if (titles[i] == "Input")
                    {
                        TestCase tc = new TestCase();
                        tc.Input = inputs[j].Replace(@"<br />", @"\n");
                        tc.Output = outputs[j].Replace(@"<br />", @"\n");
                        problem.TestCases.Add(tc);

                        ++j;
                    }
                problems.Add(problem);
            }

            return problems;
        }
    }

    public class Contest
    {
        public Contest()
        {
            Name = Url = string.Empty;
        }
        public string Name;
        public string Url;

        public bool Available;
    }
    public class Problem
    {
        public Problem()
        {
            Name = string.Empty;
            TestCases = new List<TestCase>();
        }
        public string Name;
        public List<TestCase> TestCases;
    }
    public class TestCase
    {
        public TestCase()
        {
            Input = Output = string.Empty;
        }
        public string Input;
        public string Output;
    }

    public class Database
    {
        public string path = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Visual Studio 2012\Addins");

        public static Database Instance = new Database();

        private Database()
        {
            proxy = port = username = password = string.Empty;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            ReadFromFile();
        }

        private string proxy;
        public string Proxy
        {
            get { return proxy; }
            set { proxy = value; WriteToFile(); }
        }

        private string port;
        public string Port
        {
            get { return port; }
            set { port = value; WriteToFile(); }
        }

        private bool use_proxy;
        public bool UseProxy
        {
            get { return use_proxy; }
            set { use_proxy = value; WriteToFile(); }
        }

        private string username;

        public string Username
        {
            get { return username; }
            set { username = value; WriteToFile(); }
        }

        private string password;

        public string Password
        {
            get { return password; }
            set { password = value; WriteToFile(); }
        }

        private bool use_credentials;

        public bool UseCredentials
        {
            get { return use_credentials; }
            set { use_credentials = value; WriteToFile(); }
        }

        public void ReadFromFile()
        {
            string file = Path.Combine(path, "codeforces_add_in_db.amn");
            Console.WriteLine(file);
            if (File.Exists(file))
            {
                try
                {
                    StreamReader reader = new StreamReader(file);
                    use_proxy = bool.Parse(reader.ReadLine());
                    proxy = reader.ReadLine();
                    port = reader.ReadLine();
                    use_credentials = bool.Parse(reader.ReadLine());
                    username = reader.ReadLine();
                    password = reader.ReadLine();
                    reader.Close();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
        }

        public void WriteToFile()
        {
            string file = Path.Combine(path, "codeforces_add_in_db.amn");
            StreamWriter writer = new StreamWriter(file, false);
            writer.WriteLine(use_proxy.ToString());
            writer.WriteLine(proxy);
            writer.WriteLine(port);
            writer.WriteLine(use_credentials.ToString());
            writer.WriteLine(username.ToString());
            writer.WriteLine(password.ToString());
            writer.Close();
        }

        public override string ToString()
        {
            return "" + use_proxy + " " + proxy + " " + port + " "
                + use_credentials + " " + username + " " + password;
        }

    }

    public static class Extentions
    {
        public static WebClient Init(this WebClient wc)
        {
            if (Database.Instance.UseProxy)
            {
                int port = 0;
                if (int.TryParse(Database.Instance.Port, out port))
                    wc.Proxy = new WebProxy(Database.Instance.Proxy, port);
            }
            if (Database.Instance.UseCredentials)
            {
                wc.Credentials = new NetworkCredential(Database.Instance.Username, Database.Instance.Password);
                wc.Proxy.Credentials = new NetworkCredential(Database.Instance.Username, Database.Instance.Password);
            }

            return wc;
        }
        public static HttpWebRequest Init(this HttpWebRequest wr)
        {
            if (Database.Instance.UseProxy)
            {
                int port = 0;
                if (int.TryParse(Database.Instance.Port, out port))
                    wr.Proxy = new WebProxy(Database.Instance.Proxy, port);
            }
            if (Database.Instance.UseCredentials)
            {
                wr.Credentials = new NetworkCredential(Database.Instance.Username, Database.Instance.Password);
                wr.Proxy.Credentials = new NetworkCredential(Database.Instance.Username, Database.Instance.Password);
            }

            return wr;
        }

        public static List<string> ParseHtml(this string page, string begin, string end)
        {
            List<string> fractions = new List<string>();


            for (int i = 0; i < page.Length - begin.Length - end.Length - 1; )
            {
                int start_inx = page.IndexOf(begin, i);
                if (start_inx != -1)
                {
                    int end_inx = page.IndexOf(end, start_inx + begin.Length);
                    if (end_inx != -1)
                    {
                        fractions.Add(page.Substring(start_inx + begin.Length, end_inx - start_inx - begin.Length));
                        i = end_inx + end.Length;
                    }
                    else
                        ++i;
                }
                else
                    ++i;
            }

            return fractions;
        }
    }
}
