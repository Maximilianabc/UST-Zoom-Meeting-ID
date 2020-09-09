using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;

namespace UST_Zoom_Meeting_ID
{
    public class Major
    {
        public string Abbr { get; set; }
        public List<Course> Courses { get; set; } = new List<Course>();
    }
    public class Course
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Time { get; set; }
        public string ZoomID { get; set; }
        public string Link { get; set; }
    }
    class Program
    {
        static List<Major> majors = new List<Major>();
        static string defaultpath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\UST Zoom Meeting ID\Zoom ID.json";

        enum HelpType { Main, Fetch, Load, Find, Join }
        static List<string> mainhelp => new List<string>()
        {
            "\t{fetch | load} [<PATH>]",
            "\tfind <COURSE-DETAILS>...",
            "\tjoin <COURSE-DETAILS>...",
            "\t[<COMMAND-NAME>] (-h | --help)"
        };
        static List<string> fetchhelp => new List<string>()
        {
            "\t\"fetch\" [<path>]"
        };
        static List<string> loadhelp => new List<string>()
        {
            "\t\"load\" [<path>]"
        };
        static List<string> findhelp => new List<string>()
        {
            "\t\"find\" {-a | <course-details>...} [-x <column>...]",
        };
        static List<string> joinhelp => new List<string>()
        {
            "\t\"join\" <link>",
        };

        static List<(string cmd, string desc)> options => new List<(string, string)>()
        {
            ("fetch", "Fetch IDs from the website and save the result to local json file."),
            ("load","Load IDs from local json file."),
            ("find","Find course(s) with query parameters provided."),
            ("join","Join the zoom meeting with course details provided."),
            ("-h, --help","Provide help information for this tool.")
        };
        static List<(string cmd, string desc)> fetchopts => new List<(string, string)>()
        {
            ("path","Specify the path for storing the data fetched/loading data. [Default: %AppData%/UST Zoom Meeting ID/Zoom ID.json]"),
            ("-h, --help","Provide help information for this command."),
        };
        static List<(string cmd, string desc)> queryopts => new List<(string, string)>()
        {
            ("-a, --all", "List all courses. Cannot be used with other course details."),
            ("-m <MAJOR>, --major <MAJOR>", "Specifies the major."),
            ("-c <CODE>, --code <CODE>", "Specifies the course code."),
            ("-n <NAME>, --name <NAME>","Specifies the course name."),
            ("-t <TIME>, --time <TIME>","Specifies the starting time, in the format wwwwhhmm. e.g. -t 01031300 for Mon Wed 13:00"),
            ("-z <ID>, --zoom-id <ID>","Specifies the zoom ID, which is either 9 or 10 digits."),
            ("-l <LINK>, --link <LINK>","Specifies the link."),
            ("-x <COLUMN>..., --hide <COLUMN>...", "Skip displaying course detail columns by specifying their shorthands, e.g. -x nl for skip displaying course names and links. Not a course detail, can be used with -a."),
            ("-h, --help","Provide help information for this command.")
        };
        static List<(string cmd, string desc)> joinopts => new List<(string, string)>()
        {
            ("link","Specify the link for the zoom meeting to be joined."),
            ("-h, --help","Provide help information for this command."),
        };

        static void Main(string[] args)
        {
            Action a = (Action)(args[0] switch
            {
                "fetch" => () => { Fetch(args.Length > 1 ? args[1] : ""); },
                "load" => () => { ReadLocal(args.Length > 1 ? args[1] : ""); },
                "find" => () =>
                {
                    if (args.Length < 2)
                    {
                        Help(HelpType.Find);
                    }
                    else
                    {
                        Query(args[1]);
                    }
                },
                _ => () => { Help(); }
            });
            if (args.Length == 0) 
            { 
                Help(); 
            }
            else
            {
                a();
            }
            string line = Console.ReadLine();
            while (line != "exit")
            {
                args = line.Split(new char[] { ' ' });
                a();
                Console.WriteLine("Type 'exit' to quit.");
                line = Console.ReadLine();
            }
        }

        static string SendRequest(string URL)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(URL);
            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            string ress;
            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                ress = sr.ReadToEnd();
            }
            return ress;
        }
        static string POSTRequest(string URL, string Data)
        {
            byte[] databytes = new ASCIIEncoding().GetBytes(Data);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(URL);
            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
            req.Method = "POST";
            req.KeepAlive = true;
            req.Host = "cas.ust.hk";
            req.Referer = "https://cas.ust.hk/cas/login?service=https%3A%2F%2Fitscapps.ust.hk%2Fzoom%2Fupcoming.php";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = databytes.Length;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.83 Safari/537.36";
            req.CookieContainer = new CookieContainer();

            using (Stream stream = req.GetRequestStream())
            {
                stream.Write(databytes, 0, databytes.Length);
            }

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            string ress;
            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                ress = sr.ReadToEnd();
            }
            return ress;
        }
        static void Help(HelpType help = HelpType.Main)
        {
            List<string> helps = help switch
            {
                HelpType.Main => mainhelp,
                HelpType.Fetch => fetchhelp,
                HelpType.Load => loadhelp,
                HelpType.Find => findhelp,
                HelpType.Join => joinhelp,
                _ => throw new NotImplementedException()
            };
            DescHelp(helps);
            Console.WriteLine();

            List<(string, string)> opts = help switch
            {
                HelpType.Main => options,
                HelpType.Fetch => fetchopts,
                HelpType.Load => fetchopts,
                HelpType.Find => queryopts,
                HelpType.Join => joinopts,
                _ => throw new NotImplementedException()
            };
            PadHelp(opts);
        }
        static void Fetch(string path = "")
        {
            Stopwatch w = new Stopwatch();
            w.Start();

            string baseURL = "https://itscapps.ust.hk/zoom/upcoming.php";
            string redirect = SendRequest(baseURL);

            while (redirect.Contains("formsAuthenticationArea")) // CAS login
            {
                Console.WriteLine("Login required.");
                Console.WriteLine("Please provide your ITSC account name:");
                string Username = Console.ReadLine();
                Console.WriteLine("Please provide your password:");
                string pw = Console.ReadLine();
                string exec = Regex.Match(redirect, @"execution"" value=""([\w-=]+)").Groups[1].Value;
                redirect = POSTRequest(
                    "https://cas.ust.hk/cas/login?service=https%3A%2F%2Fitscapps.ust.hk%2Fzoom%2Fupcoming.php",
                    $"execution={HttpUtility.UrlEncode(exec)}&_eventId=submit&geolocation=&username={HttpUtility.UrlEncode(Username)}&password={HttpUtility.UrlEncode(pw)}");
            }

            MatchCollection mc = Regex.Matches(redirect, @"<tr>\s+<td>(([A-Z]{4})[0-9A-Z]+)<\/td>\s+<td>(?:(\d+-\d+-\d+\s\d+:\d+)|(Webinar))<\/td>\s+<td>(.*?)<\/td>\s+<td nowrap>(\d+)<\/td>\s+<td><a href=""([\w:\/.?=]+)", RegexOptions.Singleline);
            foreach (Match m in mc)
            {
                string code = m.Groups[1].Value;
                string major = m.Groups[2].Value;
                string time = string.IsNullOrEmpty(m.Groups[3].Value) ? m.Groups[4].Value : m.Groups[3].Value;
                string name = m.Groups[5].Value;
                string id = m.Groups[6].Value;
                string link = m.Groups[7].Value;

                Major maj = majors.Find(mm => mm.Abbr == major);
                if (maj == null)
                {
                    majors.Add(new Major()
                    {
                        Abbr = major,
                        Courses = new List<Course>()
                        {
                            new Course()
                            {
                                Code = code,
                                Time = time,
                                Name = name,
                                ZoomID = id,
                                Link = link
                            }
                        }
                    });
                }
                else
                {
                    maj.Courses.Add(new Course()
                    {
                        Code = code,
                        Time = time,
                        Name = name,
                        ZoomID = id,
                        Link = link
                    });
                }
            }
            foreach (Major maj in majors)
            {
                maj.Courses = maj.Courses.OrderBy(c => c.Code).ThenBy(c => c.Name).ToList();
            }
            majors = majors.OrderBy(m => m.Abbr).ToList();

            string Json = JsonSerializer.Serialize(majors, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(path, Json);
            w.Stop();

            Console.WriteLine($"Fetched sucessfully in {Math.Round(w.ElapsedMilliseconds / 1000D, 3)} second(s). The result is located at {path}.");
        }
        static void Query(string q)
        {

        }
        static void Join()
        {

        }
        static void ReadLocal(string path = "")
        {
            try
            {
                if (path == "") path = defaultpath;
                majors = JsonSerializer.Deserialize<List<Major>>(File.ReadAllText(path));
            }
            catch (FileNotFoundException)
            {
                string option = "";
                while (option != "Y" || option != "N")
                {
                    Console.WriteLine("Corresponding file is not located at default location. Do you wanna specify the path instead? (Y/N)");
                    option = Console.ReadLine();
                    if (option == "Y")
                    {
                        OpenFileDialog d = new OpenFileDialog() { Filter = "JSON files (*.json)"};
                        d.ShowDialog();
                        try
                        {
                            majors = JsonSerializer.Deserialize<List<Major>>(File.ReadAllText(d.FileName));
                        }
                        catch
                        {
                            Console.WriteLine("Unknown error occurred.");
                        }
                    }
                }            
            }
            catch
            {
                Console.WriteLine("Unknown error occurred.");
            }
        }
        static void DescHelp(List<string> helps)
        {
            Console.WriteLine("Usage: ");
            foreach (string help in helps)
            {
                Console.WriteLine(help);
            }
            Console.WriteLine();
        }
        static void PadHelp(List<(string,string)> options, int pad = 20, int maxlength = 50)
        {
            foreach ((string cmd, string desc) opt in options)
            {
                if (opt.desc.Length > maxlength)
                {
                    int lastspace = 0;
                    while (lastspace < opt.desc.Length)
                    {
                        int oldlast = lastspace;
                        if (oldlast + maxlength > opt.desc.Length)
                        {
                            Console.WriteLine($"{new string(' ', pad)}{opt.desc.Substring(oldlast)}");
                            break;
                        }
                        else
                        {
                            lastspace = Regex.Matches(opt.desc, @"\s").Cast<Match>().Select(m => m.Index).Where(ind => ind <= lastspace + maxlength).Max();
                            string firstcol = oldlast == 0 ? $"{opt.cmd.PadRight(pad)}" : new string(' ', pad);
                            Console.WriteLine($"{firstcol}{opt.desc.Substring(oldlast, lastspace - oldlast)}");
                            lastspace++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{opt.cmd.PadRight(pad)}{opt.desc}");
                }
            }
        }
        static void PadTable(int[] pads, int[] maxlengths)
        {

        }
    }
}
