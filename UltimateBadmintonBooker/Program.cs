using CommandLine;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UltimateBadmintonBooker
{
    class Options
    {
        [Option('n', "CourtsToBook", Required = true, HelpText = "Number of courts to book. Requires equal number of accounts.")]
        public int CourtsToBook { get; set; }
        [Option('d', "BookingDate", Required = true, HelpText = "In the format of 19_11_2019.")]
        public string BookingDate { get; set; }
        [Option('t', "StartTime", Required = true, HelpText = "In the format of 6_00 or 20_00.")]
        public string StartTime { get; set; }
        [Option('u', "UserNames", Separator = ',', Required = true, HelpText = "Username logins.")]
        public IList<string> UserNames { get; set; }
        [Option('p', "Passwords", Separator = ',', Required = true, HelpText = "Login passwords.")]
        public IList<string> Passwords { get; set; }
        [Option('r', "ReverseBooking", Required = false, HelpText = "Book available courts in reverse order.")]
        public bool ReverseBooking { get; set; } = false;
    }

    class BrowserInstance 
    {
        static int _succeeded = 0;
        IWebDriver _driver;
        string _username = "";
        string _password = "";
        int _browserInstance;
        bool _interrupt = false;

        public BrowserInstance(int courtOffset, string username, string password)
        {
            _browserInstance = courtOffset;
            _username = username;
            _password = password;
        }

        public async Task RunAsync()
        {
            await Task.Run(Run);
        }

        public void Run()
        {
            _driver = new ChromeDriver(AppDomain.CurrentDomain.BaseDirectory, 
                new ChromeOptions() { LeaveBrowserRunning = true });
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);

            Login();

            bool result = false;
            while (!result && !_interrupt)
            {
                result = TryBookCourt();
                if(result)
                {
                    result = TrySubmitCart(); 
                }               
                if (!result)
                    Thread.Sleep(300);
            }
            if (result)
                Interlocked.Increment(ref _succeeded);
        }

        public void Interrupt()
        {
            _interrupt = true;
        }

        public bool TryBookCourt()
        {
            try
            {
                if (TryGetBookingTime()) TryAddCourtToCart();
                else return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        void TryAddCourtToCart()
        {
            var addToBasketBtn = _driver.FindElement(By.XPath(".//*[@id='basketControl_1_1']"));
            if (addToBasketBtn.GetAttribute("class").Contains("sr_TooEarly"))
            {
                throw new Exception("Too early to book courts.");
            }

            addToBasketBtn.Click();
            var courtSelector = new SelectElement(_driver.FindElement(By.XPath(".//*[@id='subLocSelect']")));
            int optionCount = courtSelector.Options.Count;
            int index = -1;
            if (optionCount > 0)
            {
                var lastIndex = optionCount - 1;
                var rawIndex = Program.Options.ReverseBooking ? lastIndex - _browserInstance + _succeeded : _browserInstance - _succeeded;
                index = Math.Min(Math.Max(rawIndex, 0), lastIndex);
                courtSelector.SelectByIndex(index);
            }
            Console.WriteLine($"Instance {_browserInstance} found {optionCount} courts available. Selected: {(index == -1 ? "None" : index.ToString())}");
            var submitCourtBtn = _driver.FindElement(By.XPath("//span[text()='OK']"));
            submitCourtBtn.Click();
        }

        bool TrySubmitCart()
        {
            try
            {
                var termsToggle = _driver.FindElement(By.XPath(".//*[@id='TermsAccepted']"));
                termsToggle.Click();
                termsToggle.Submit();
            } catch(Exception e)
            {
                Console.Error.WriteLine(e.Message + "\n" + e.StackTrace);
                return false;
            }

            return true;
        }

        void Login()
        {
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
            _driver.Url = LoginUrl();
            var usernameField = _driver.FindElement(By.XPath(".//*[@id='UserName']"));
            var passwordField = _driver.FindElement(By.XPath(".//*[@id='Password']"));
            usernameField.SendKeys(_username);
            passwordField.SendKeys(_password);
            passwordField.Submit();
        }

        bool TryGetBookingTime()
        {
            _driver.Url = SearchPageUrl(Program.Options.BookingDate, Program.Options.StartTime, "22_00");
            try
            {
                _driver.FindElement(By.XPath(".//*[@id='NoSearchResultsNotice']"));
            }
            catch (Exception)
            {
                try
                {
                    while (_driver.FindElement(By.XPath(".//*[text()='Retrieving']")) != null) Thread.Sleep(100);
                }
                catch (Exception)
                {
                    return true;
                }
                return true;
            }

            return false;
        }

        string SearchPageUrl(string date, string startTime, string endTime)
        {
            return $"https://bookwkg.freedom-leisure.co.uk/kingalfredbookings/Search/PerformSearch?SiteID=1&Activity=0111&EnglishDate={date}&English_TimeFrom={startTime}&English_TimeTo={endTime}&Duration=120&submitButton=Search";
        }

        string LoginUrl()
        {
            return $"https://bookwkg.freedom-leisure.co.uk/kingalfredbookings/Account/LogOn";
        }
    }

    class Program
    {
        public static Options Options;
        static List<BrowserInstance> _instances = new List<BrowserInstance>();

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => Initialize(opts))
                .WithNotParsed((errs) => HandleParseError(errs));
            Task[] tasks = new Task[Options.CourtsToBook];
            for (int i = 0; i < tasks.Length; i++)
            {
                var browser = new BrowserInstance(i, Options.UserNames[i], Options.Passwords[i]);
                _instances.Add(browser);
                tasks[i] = browser.RunAsync();
            }

            while (UpdateInput()) { };
            for (int i = 0; i < tasks.Length; i++)
                _instances[i].Interrupt();
            Task.WaitAll(tasks);
        }

        static bool UpdateInput()
        {
            if (Console.ReadKey().Key == ConsoleKey.Escape)
            {
                Console.WriteLine("Quit!");
                return false;
            }

            return true;
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            foreach (var err in errs)
            {
                Console.WriteLine(err);
            }
        }

        private static void Initialize(Options opts)
        {
            Options = opts;
        }        
    }
}
