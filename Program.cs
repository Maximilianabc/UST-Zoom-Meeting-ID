using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace UST_Zoom_Meeting_ID
{
    public class Settings
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Default_Path { get; set; }
        public string Favourite_Path { get; set; }
    }
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
        static string folder = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\UST Zoom Meeting ID\";
        static string configpath = $"{folder}Config.json";

        static Settings settings = new Settings() { Default_Path = $"{folder}Zoom ID.json" };
        static List<Major> majors = new List<Major>();
        static List<Major> favlist = new List<Major>();

        enum HelpType { Main, Fetch, Find, Mine, Config }
        static List<string> mainhelp => new List<string>()
        {
            "\tfetch [<PATH>]",
            "\tfind <COURSE-DETAILS>...",
            "\tconfig <CONFIG-OPTIONS>...",
            "\t[<COMMAND-NAME>] (-h | --help)"
        };
        static List<string> fetchhelp => new List<string>()
        {
            "\t\"fetch\" [<path>] [-s | --save-password]"
        };
        static List<string> findhelp => new List<string>()
        {
            "\t\"find\" {-a | <course-details>...} [<display-options> <columns>...]",
        };
        static List<string> minehelp => new List<string>()
        {
            "\t\"mine\" join <course-details>...",
            "\t\"mine\" {add | remove} <course-details>...",
        };
        static List<string> confighelp => new List<string>()
        {
            "\t\"config\" [-u=<username>] [-p=<password>] [-d=<default-path>]"
        };

        static List<ITuple> options => new List<ITuple>()
        {
            ("fetch", "Fetch IDs from the website and save the result to local json file."),
            ("find","Find course(s) with query parameters provided."),
            ("-h, --help","Provide help information for this tool.")
        };
        static List<ITuple> fetchopts => new List<ITuple>()
        {
            ("path","Specify the path for storing the data fetched/loading data. [Default: the path specified in %AppData%/UST Zoom Meeting ID/Config.json]"),
            ("-h, --help","Provide help information for this command."),
        };
        static List<ITuple> queryopts => new List<ITuple>()
        {
            ("-a, --all", "List all courses. Cannot be used with other course details."),
            ("Coures Details:",""),
            ("-m=<MAJOR>, --major=<MAJOR>", "Specifies the major."),
            ("-c=<CODE>, --code=<CODE>", "Specifies the course code. The major abbreviation can be skipped if -m is used. e.g. -m=COMP -c=2011"),
            ("-n=<NAME>, --name=<NAME>","Specifies the course name. Use quotation marks if spaces are present in the name."),
            ("-t=<TIME>, --time=<TIME>","Specifies the starting time, in the format ww[ww]hhmm. e.g. -t=01031300 for Mon Wed 13:00, -t=021430 for Tue 14:30"),
            ("-z=<ID>, --zoom-id=<ID>","Specifies the zoom ID, which is either 9 or 10 digits."),
            ("-l=<LINK>, --link=<LINK>","Specifies the link."),
            ("Display Options:",""),
            ("-x=<COLUMN>..., --hide=<COLUMN>...", "Skip displaying course detail columns by specifying their shorthands, e.g. -x=nl for skip displaying course names and links."),
            ("-s=<COLUMN>..., --sort=<COLUMN>...", "Sort course detail columns by specifying their shorthands, e.g. -s=cn for sorting courses by codes then by names."),
            ("Operators:",""),
            ("*","Wildcard. e.g. -m=C*** returns all courses of which major names start with C"),
            ("?","Null coalesce. e.g. -c=COMP1***? returns all courses of which course codes start with COMP1, with or without the last alphabet."),
            ("~","Approxiamte match. e.g. -n=~Lab returns all courses with names contain \"Lab\""),
            ("\"","Grouping characters. e.g. -n=\"ACCT2010 - L07\""),
            ("&","And. e.g. -n=~\"Lab\"&~\"Programming\" returns all courses contain \"Lab\" and \"Programming\""),
            ("|","Or. e.g. -m=\"COMP\"|\"MATH\" returns all COMP and MATH courses."),
            ("!","Not. e.g. -n=!~Calculus returns all courses of which names do not contain \"Calculus\"."),
            ("-h, --help","Provide help information for this command.")
        };
        static List<ITuple> mineopts => new List<ITuple>()
        {
            ("add","Add courses to favourite list with course details provided."),
            ("remove","Remove courses from favourite list with course details provided."),
            ("join","Join the zoom meeting on the favourite list with course details provided."),
            ("-h, --help","Provide help information for this command."),
        };
        static List<ITuple> configopts => new List<ITuple>()
        {
            ("save", "Save the corresponding configs"),
            ("remove", "Remove the corresponding configs"),
            ("-u, --username", "ITSC username."),
            ("-p, --password", "ITSC password."),
            ("-d, --default-path", "Default path for saving/loading results fetched."),
        };

        static List<int> widths => new List<int>() { 15, 15, 15, 15, 15 };
        static List<int> maxlengths => new List<int>() { 20, 20, 20, 20, 20 };

        static void Main(string[] args)
        {
            ReadConfig();
            ReadLocal();
            Action a = () =>
            {
                if (args.Length == 0)
                {
                    Help();
                }
                else
                {
                    ((Action)(args[0] switch
                    {
                        "fetch" => () => { Fetch(args.Length >= 2 ? args[1] : ""); },
                        "find" => () =>
                        {
                            if (args.Length < 2)
                            {
                                Help(HelpType.Find);
                            }
                            else
                            {
                                Query(args.Skip(1).ToArray());
                            }
                        },
                        "mine" => () =>
                        {
                            if (args.Length < 2)
                            {
                                Help(HelpType.Mine);
                            }
                            else
                            {
                                Mine(args.Skip(1).ToArray());
                            }
                        },
                        "config" => () =>
                        {
                            if (args.Length < 2)
                            {
                                Help(HelpType.Config);  
                            }
                            else
                            {
                                EditConfig(args.Skip(1).ToArray());
                            }
                        },
                        _ => () => { Help(); }
                    }))();
                }
            };
            for (string line; (line = Console.ReadLine()) != "exit";)
            {
                args = line.Split(new char[] { ' ' });
                a();
                Console.WriteLine("Type 'exit' to quit.");
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

        static void Fetch(string path = "")
        {
            string baseURL = "https://itscapps.ust.hk/zoom/upcoming.php";
            string redirect = SendRequest(baseURL);

            int count = 0;
            while (redirect.Contains("formsAuthenticationArea")) // CAS login
            {
                if (count >= 1)
                {
                    Console.WriteLine("Login failed. Please check your credentials.");
                    return;
                }
                string Username = settings.Username;
                string pw = settings.Password;
                Console.WriteLine("Login required.");
                if (string.IsNullOrEmpty(Username))
                {
                    Console.WriteLine("Please provide your ITSC account name:");
                    Username = Console.ReadLine();
                }
                if (string.IsNullOrEmpty(pw))
                {
                    Console.WriteLine("Please provide your password:");
                    pw = Console.ReadLine();
                }
                string exec = Regex.Match(redirect, @"execution"" value=""([\w-=]+)").Groups[1].Value;
                redirect = POSTRequest(
                    "https://cas.ust.hk/cas/login?service=https%3A%2F%2Fitscapps.ust.hk%2Fzoom%2Fupcoming.php",
                    $"execution={HttpUtility.UrlEncode(exec)}&_eventId=submit&geolocation=&username={HttpUtility.UrlEncode(Username)}&password={HttpUtility.UrlEncode(pw)}");
                count++;
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
                    Course c = maj.Courses.Find(cc => cc.Code == code && cc.Name == name && cc.ZoomID == id && cc.Link == link);
                    if (c != null)
                    {
                        c.Time += $"\r\n{time}";
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
            }
            foreach (Major maj in majors)
            {
                maj.Courses = maj.Courses.OrderBy(c => c.Code).ThenBy(c => c.Name).ToList();
            }
            majors = majors.OrderBy(m => m.Abbr).ToList();

            string Json = JsonSerializer.Serialize(majors, new JsonSerializerOptions() { WriteIndented = true });
            if (string.IsNullOrEmpty(path))
            {
                path = settings.Default_Path;
                if (!Directory.Exists(Path.GetDirectoryName(settings.Default_Path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(settings.Default_Path));
                }
            }        
            File.WriteAllText(path, Json);
            Console.WriteLine($"Fetched sucessfully. The result is located at {path}.");
        }
        static void Query(string[] args)
        {
            
        }
        static void Mine(string[] args)
        {

        }

        static void ReadLocal(string path = "")
        {
            try
            {
                if (string.IsNullOrEmpty(path)) path = settings.Default_Path;
                majors = JsonSerializer.Deserialize<List<Major>>(File.ReadAllText(path));
                Console.WriteLine($"{majors.Count} majors loaded.");
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                string option = "";
                while (option != "Y" || option != "N")
                {
                    Console.WriteLine("Corresponding file is not located at default location. Do you wanna specify the path instead? (Y/N)");
                    option = Console.ReadLine();
                    if (option == "Y")
                    {
                        Console.WriteLine("Please enter the path of the json file");
                        path = Console.ReadLine();
                        try
                        {
                            majors = JsonSerializer.Deserialize<List<Major>>(File.ReadAllText(path));
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
        static void ReadConfig()
        {
            if (!Directory.Exists(folder))
            { 
                Directory.CreateDirectory(folder);               
            }
            else
            {
                if (!File.Exists(configpath))
                {
                    File.WriteAllText(configpath, JsonSerializer.Serialize(settings, new JsonSerializerOptions() { WriteIndented = true }));
                }
                else
                {
                    settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(configpath));
                }
            }
        }
        static void EditConfig(string[] args)
        {
            foreach (string arg in args)
            {
                string opt = arg.Split(new char[] { '=' })[0];
                string subarg = arg.Split(new char[] { '=' })[1];
                switch (opt)
                {
                    case "-u":
                        settings.Username = subarg;
                        break;
                    case "-p":
                        settings.Password = subarg;
                        break;
                    case "-d":
                        settings.Default_Path = subarg;
                        break;
                    default:
                        Help(HelpType.Config);
                        return;
                };
            }
            File.WriteAllText(configpath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Config saved.");
        }

        static void Help(HelpType help = HelpType.Main)
        {
            List<string> helps = help switch
            {
                HelpType.Main => mainhelp,
                HelpType.Fetch => fetchhelp,
                HelpType.Find => findhelp,
                HelpType.Mine => minehelp,
                HelpType.Config => confighelp,
                _ => throw new NotImplementedException()
            };
            DescHelp(helps);
            Console.WriteLine();

            List <ITuple> opts = help switch
            {
                HelpType.Main => options,
                HelpType.Fetch => fetchopts,
                HelpType.Find => queryopts,
                HelpType.Mine => mineopts,
                HelpType.Config => configopts,
                _ => throw new NotImplementedException()
            };
            PadTable(opts, new List<int>() { 50,50 }, new List<int>() { 50,50 });
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
        static void PadTable(List<ITuple> table, List<int> pads, List<int> maxlengths)
        {
            int numelement = table.First().Length;
            if (pads.Count != numelement || maxlengths.Count != numelement) throw new ArgumentException();

            List<List<List<string>>> formattedtable = new List<List<List<string>>>();
            for (int i = 0; i < table.Count; i++)
            {
                List<List<string>> formattedrow = new List<List<string>>(numelement);
                for (int j = 0; j < numelement; j++)
                {
                    formattedrow.Add(PadCell(table[i][j].ToString(), pads[j], maxlengths[j]));
                }
                int maxrows = formattedrow.Select(r => r.Count).Max();
                int colindex = 0;
                foreach(List<string> contentrow in formattedrow)
                {
                    if (contentrow.Count < maxrows)
                    {
                        contentrow.AddRange(Enumerable.Range(0, maxrows - contentrow.Count + 1).Select(index => new string(' ', pads[colindex])));
                    }
                    colindex++;
                }
                for (int k = 0; k < maxrows; k++)
                {
                    for (int l = 0; l < numelement; l++)
                    {
                        Console.Write(formattedrow[l][k]);
                    }
                    Console.Write(Environment.NewLine);
                }
            }
        }
        static List<string> PadCell(string cellcontent, int pad, int maxlength)
        {
            List<string> rows = new List<string>();
            if (cellcontent.Length > maxlength)
            {
                int lastspace = 0;
                while (lastspace < cellcontent.Length)
                {
                    int oldlast = lastspace;
                    if (oldlast + maxlength > cellcontent.Length)
                    {
                        rows.Add(cellcontent.Substring(oldlast).PadRight(pad));
                        break;
                    }
                    else
                    {
                        lastspace = Regex.Matches(cellcontent, @"\s").Cast<Match>().Select(m => m.Index).Where(ind => ind <= oldlast + maxlength).Max();
                        rows.Add(cellcontent.Substring(oldlast, lastspace - oldlast).PadRight(pad));
                        lastspace++;
                    }
                }
            }
            else
            {
                rows.Add(cellcontent.PadRight(pad));
            }
            return rows;
        }
    }
}
