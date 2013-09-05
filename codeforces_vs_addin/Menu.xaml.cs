using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using EnvDTE;
using EnvDTE80;

namespace codeforces_vs_addin
{
    public partial class Menu : UserControl
    {
        public static Menu Instance;
        public Menu()
        {
            InitializeComponent();
            Instance = this;
        }
    }

    public class ContestsViewModel : IViewModel
    {
        public static ContestsViewModel Instance;
        public ContestsViewModel()
        {
            Instance = this;
            Contests = new ObservableCollection<ContestCdt>();
            CheckCommand = new IDelegateCommand((obj) =>
            {
                Menu.Instance.Dispatcher.InvokeAsync(() =>
                {
                    Status = "checking contests ...";
                    CheckBtnContent = "checking ...";
                    bool exception_occured = false;
                    try
                    {
                        CheckCommand.CanExec = false;
                        var contests = WebReader.ReadContestsAll("http://codeforces.com/contests");
                        var collection = ContestsViewModel.Instance.Contests;
                        collection.Clear();
                        for (int i = 0; i < contests.Count; ++i)
                            collection.Add(new ContestCdt(contests[i]));
                    }
                    catch (Exception e)
                    {
                        Status = e.Message;
                        exception_occured = true;
                    }
                    if (!exception_occured)
                        Status = "checking contests finished :)";
                    CheckBtnContent = "check !";
                    CheckCommand.CanExec = true;
                });
            });
        }

        public ObservableCollection<ContestCdt> Contests { get; set; }
        public IDelegateCommand CheckCommand { get; set; }

        private string status = string.Empty;
        public string Status
        {
            get { return status; }
            set { status = value; Notify("Status"); }
        }

        private string check_btn_content = "check !";
        public string CheckBtnContent
        {
            get { return check_btn_content; }
            set { check_btn_content = value; Notify("CheckBtnContent"); }
        }


    }

    public class ProblemsViewModel : IViewModel
    {
        public static ProblemsViewModel Instance;
        public ProblemsViewModel()
        {
            Instance = this;
            Problems = new ObservableCollection<ProblemCdt>();
        }

        public ObservableCollection<ProblemCdt> Problems { get; set; }

        private string status = string.Empty;
        public string Status
        {
            get { return status; }
            set { status = value; Notify("Status"); }
        }

    }

    public class ContestCdt : IViewModel
    {
        public Contest c;
        public ContestCdt(Contest contest)
        {
            c = contest;
            CheckCommand = new IDelegateCommand((object obj) =>
            {
                Menu.Instance.Dispatcher.InvokeAsync(() =>
                {
                    var cs = ContestsViewModel.Instance;
                    cs.Status = "checking for problems ...";
                    CheckCommand.CanExec = false;
                    bool exception_accured = false;
                    try
                    {
                        List<Problem> probs = WebReader.ReadProblemsAll(c);
                        var col = ProblemsViewModel.Instance.Problems;
                        col.Clear();
                        for (int i = 0; i < probs.Count; ++i)
                            col.Add(new ProblemCdt(probs[i],c));
                    }
                    catch (Exception e)
                    {
                        cs.Status = e.Message;
                    }
                    CheckCommand.CanExec = true;
                    if (!exception_accured)
                    {
                        cs.Status = "checking problems finished :)";
                        ProblemsViewModel.Instance.Status = "problems for " + c.Name;
                    }
                });
            });
        }

        public string Name
        {
            get { return c.Name; }
            set
            {
                c.Name = value;
                Notify("Name");
            }
        }
        public string Url
        {
            get { return c.Url; }
            set
            {
                c.Url = value;
                Notify("Url");
            }
        }

        public bool Available
        {
            get { return c.Available; }
            set
            {
                c.Available = value;
                Notify("Background");
            }
        }

        public Brush Background
        {
            get
            {
                if (c.Available)
                    return new SolidColorBrush(Colors.GreenYellow);
                return new SolidColorBrush(Colors.Yellow);
            }
        }

        public IDelegateCommand CheckCommand { get; set; }
    }

    public class ProblemCdt : IViewModel
    {
        public Contest c;
        public Problem p;
        public ProblemCdt(Problem problem,Contest contest)
        {
            p = problem;
            c = contest;

            GenerateCommand = new IDelegateCommand((obj) =>
            {
                var app = Connect.applicationObject;
                var ps = ProblemsViewModel.Instance;
                var proj = app.GetActiveProject();
                if (proj == null)
                {
                    string msg = "first open a project please !";
                    if (ps.Status.Contains(msg))
                        ps.Status = ps.Status + "!";
                    else
                        ps.Status = msg;
                    return;
                }
                string folder = System.IO.Path.GetDirectoryName(proj.FullName);
                folder = System.IO.Path.Combine(folder, c.Name.SafePath());
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                foreach (ProjectItem pi in proj.ProjectItems)
                    if (pi.Name == p.Name.SafePath())
                        pi.Remove();

                string cpp = System.IO.Path.Combine(folder, p.Name.SafePath() + ".cpp");
                StreamWriter sw = new StreamWriter(cpp, false);
                WriteTemplate(sw);
                sw.Close();

                proj.ProjectItems.AddFromFile(cpp).Open();

            });

            ShowInBrowser = new IDelegateCommand((obj) =>
            {
                bool exception_accured = false;
                string browser = string.Empty;
                try
                {
                    browser = "chrome";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chrome.exe",c.Url + "/problem/" + p.Name.First()));
                }
                catch (Exception e)
                {
                    ProblemsViewModel.Instance.Status = e.Message;
                    try{
                        browser = "ie";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",c.Url + "/problem/" + p.Name.First()));
                    }catch(Exception e2){
                        ProblemsViewModel.Instance.Status = e2.Message;
                        exception_accured = true;
                    }
                }
                if(!exception_accured)
                    ProblemsViewModel.Instance.Status = p.Name + " launched in " + browser + " .";
            });
        }

        public void WriteTemplate(StreamWriter sw)
        {
            string template = CodeTemplateViewModel.template;
            bool ok = false;
            if (File.Exists(template))
            {
                ok = true;
                try
                {
                    StreamReader sr = new StreamReader(template);
                    sw.WriteLine(sr.ReadToEnd());
                    sr.Close();
                }
                catch
                {
                    ok = false;
                }
            }

            if(!ok) // write default template !
            {
                sw.WriteLine(@"#include <functional>
#include <algorithm>
#include <iostream>
#include <fstream>
#include <sstream>
#include <numeric>
#include <cstdlib>
#include <cstring>
#include <climits>
#include <string>
#include <cstdio>
#include <vector>
#include <deque>
#include <cmath>
#include <list>
#include <set>
#include <map>
#define rep(i,m,n) for(int i=(m),_end=(n);i < _end;++i)
#define repe(i,m,n) for(int i=(m), _end =(n);i <= _end;++i)
typedef long long ll;
using namespace std;");
            }

            sw.WriteLine(@"const bool testing = true;


void program() {
	
}

int main(){
	if(!testing){ // set testing to false when submiting to codeforces
		program(); // write your program in 'program' function (its your new main !)
		return 0;
	}
	
	FILE* fin = NULL;");
            for(int i = 0; i < p.TestCases.Count; ++i)
            {
                TestCase tc = p.TestCases[i];
                sw.WriteLine("\tfin = fopen(\"in.txt\", \"w+\");");
                sw.WriteLine("\tfprintf(fin, \"{0}\");", tc.Input);
                sw.WriteLine("\tfclose(fin);");
                sw.WriteLine("\tfreopen(\"in.txt\", \"r\", stdin);");
                sw.WriteLine("\tprintf(\"test case({0}) => expected : \\n\");", i + 1);
                sw.WriteLine("\tprintf(\"{0}\");", tc.Output);
                sw.WriteLine("\tprintf(\"test case({0}) => founded  : \\n\");", i + 1);
                sw.WriteLine("\tprogram();");
            }

            sw.WriteLine();
            sw.WriteLine("\treturn 0;");
            sw.WriteLine("}");
        }

        public string Name
        {
            get { return p.Name; }
            set
            {
                p.Name = value; Notify("Name");
            }
        }

        public List<TestCase> TestCases { get { return p.TestCases; } }

        public IDelegateCommand GenerateCommand { get; set; }
        public IDelegateCommand ShowInBrowser { get; set; }
    }

    public class CodeTemplateViewModel : IViewModel
    {
        public static string path = System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Visual Studio 2012\Addins");

        public static string template
        {
            get
            {
                return System.IO.Path.Combine(path, "codeforces_vs_addin_template.txt");
            }
        }
        

        public CodeTemplateViewModel()
        {
            ChangeCommand = new IDelegateCommand((obj) =>
            {
                bool exception_auccered = false;
                try
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    if (!File.Exists(template))
                        File.Create(template).Close();
                    System.Diagnostics.Process.Start(template);
                }
                catch (Exception e)
                {
                    Status = e.Message;
                    exception_auccered = true;
                }
                if (!exception_auccered)
                    Status = "edit and save this file !";
            });
            DeleteCommand = new IDelegateCommand((obj) =>
            {

                if (File.Exists(template))
                {
                    try
                    {
                        File.Delete(template);
                        string msg = "default code template is back !";
                        if (Status.Contains(msg))
                            Status = Status + "!";
                        else
                            Status = msg;
                    }
                    catch (Exception e)
                    {
                        Status = e.Message;
                    }
                }
                else
                {
                    string msg = "you have not set a code template !";
                    if (Status.Contains(msg))
                        Status = Status + "!";
                    else
                        Status = msg;
                }
            });
        }

        public IDelegateCommand ChangeCommand { get; set; }
        public IDelegateCommand DeleteCommand { get; set; }

        private string status = string.Empty;
        public string Status
        {
            get { return status; }
            set { status = value; Notify("Status"); }
        }

    }




    public class DatabaseViewModel : IViewModel
    {
        public Database db = Database.Instance;

        private string last_error = string.Empty;

        public string LastError
        {
            get { return last_error; }
            set { last_error = value; Notify("LastError"); }
        }


        public string Proxy
        {
            get { return db.Proxy; }
            set { db.Proxy = value; Notify("Proxy"); }
        }

        public string Port
        {
            get { return db.Port; }
            set
            {
                int p;
                if (!string.IsNullOrEmpty(value) && !int.TryParse(value, out p))
                {
                    LastError = "port number should be an int !";
                    Notify("Port");
                    return;
                }
                if (!string.IsNullOrEmpty(LastError))
                    LastError = string.Empty;
                db.Port = value;
                Notify("Port");
            }
        }

        public bool UseProxy
        {
            get
            {
                return db.UseProxy;
            }
            set { db.UseProxy = value; Notify("UseProxy"); }
        }



        public string Username
        {
            get { return db.Username; }
            set { db.Username = value; Notify("Username"); }
        }

        public string Password
        {
            get { return db.Password; }
            set { db.Password = value; Notify("Password"); }
        }

        public bool UseCredentials
        {
            get { return db.UseCredentials; }
            set { db.UseCredentials = value; Notify("UseCredentials"); }
        }

    }

    public class IDelegateCommand : ICommand
    {
        public IDelegateCommand(Action<object> Action)
        {
            action = Action;
        }
        public Action<object> action;
        private bool can_exec = true;

        public bool CanExec
        {
            get { return can_exec = true; }
            set { can_exec = value; CanExecuteChanged(this, new EventArgs()); }
        }


        public bool CanExecute(object parameter)
        {
            return can_exec;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            action(parameter);
        }
    }

    public abstract class IViewModel : INotifyPropertyChanged
    {
        public void Notify(string property)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class WebReader
    {
        public static List<Contest> ReadContestsAll(string url)
        {
            List<Contest> contests = new List<Contest>();
            string page = string.Empty;

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
                problem.Name = titles[i].Trim();
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
        public string path = System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Visual Studio 2012\Addins");

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
            string file = System.IO.Path.Combine(path, "codeforces_add_in_db.amn");
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
            string file = System.IO.Path.Combine(path, "codeforces_add_in_db.amn");
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

        public static Project GetActiveProject(this DTE2 dte)
        {
            Project activeProject = null;

            Array activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
            {
                activeProject = activeSolutionProjects.GetValue(0) as Project;
            }

            return activeProject;
        }

        public static void IncludeNewFiles(this DTE2 dte2)
        {
            int count = 0;
            List<string> newfiles;

            foreach (Project project in dte2.Solution.Projects)
            {
               
                if (project.UniqueName.EndsWith(".csproj"))
                {
                    newfiles = GetFilesNotInProject(project);

                    foreach (var file in newfiles)
                        project.ProjectItems.AddFromFile(file);

                    count += newfiles.Count;
                }
            }
            dte2.StatusBar.Text = String.Format("{0} new file{1} included in the project.", count, (count == 1 ? "" : "s"));
        }

        public static List<string> GetAllProjectFiles(ProjectItems projectItems, string extension)
        {
            List<string> returnValue = new List<string>();

            foreach (ProjectItem projectItem in projectItems)
            {
                for (short i = 1; i <= projectItems.Count; i++)
                {
                    string fileName = projectItem.FileNames[i];
                    if (System.IO.Path.GetExtension(fileName).ToLower() == extension)
                        returnValue.Add(fileName);
                }
                returnValue.AddRange(GetAllProjectFiles(projectItem.ProjectItems, extension));
            }

            return returnValue;
        }

        public static List<string> GetFilesNotInProject(Project project)
        {
            List<string> returnValue = new List<string>();
            string startPath = System.IO.Path.GetDirectoryName(project.FullName);
            List<string> projectFiles = GetAllProjectFiles(project.ProjectItems, ".cs");

            foreach (var file in Directory.GetFiles(startPath, "*.cs", SearchOption.AllDirectories))
                if (!projectFiles.Contains(file)) returnValue.Add(file);

            return returnValue;
        }

        public static string SafePath(this string path)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) {
                path = path.Replace(c, '-');
            }
            return path;
        }
    }

    public static class PasswordHelper
    {
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.RegisterAttached("Password",
            typeof(string), typeof(PasswordHelper),
            new FrameworkPropertyMetadata(string.Empty, OnPasswordPropertyChanged));

        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached("Attach",
            typeof(bool), typeof(PasswordHelper), new PropertyMetadata(false, Attach));

        private static readonly DependencyProperty IsUpdatingProperty =
           DependencyProperty.RegisterAttached("IsUpdating", typeof(bool),
           typeof(PasswordHelper));


        public static void SetAttach(DependencyObject dp, bool value)
        {
            dp.SetValue(AttachProperty, value);
        }

        public static bool GetAttach(DependencyObject dp)
        {
            return (bool)dp.GetValue(AttachProperty);
        }

        public static string GetPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(PasswordProperty);
        }

        public static void SetPassword(DependencyObject dp, string value)
        {
            dp.SetValue(PasswordProperty, value);
        }

        private static bool GetIsUpdating(DependencyObject dp)
        {
            return (bool)dp.GetValue(IsUpdatingProperty);
        }

        private static void SetIsUpdating(DependencyObject dp, bool value)
        {
            dp.SetValue(IsUpdatingProperty, value);
        }

        private static void OnPasswordPropertyChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            passwordBox.PasswordChanged -= PasswordChanged;

            if (!(bool)GetIsUpdating(passwordBox))
            {
                passwordBox.Password = (string)e.NewValue;
            }
            passwordBox.PasswordChanged += PasswordChanged;
        }

        private static void Attach(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;

            if (passwordBox == null)
                return;

            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= PasswordChanged;
            }

            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += PasswordChanged;
            }
        }

        private static void PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            SetIsUpdating(passwordBox, true);
            SetPassword(passwordBox, passwordBox.Password);
            SetIsUpdating(passwordBox, false);
        }
    }


}
