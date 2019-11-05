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
    }

    class BrowserInstance 
    {
        IWebDriver _driver;
        string _username = "";
        string _password = "";
        int _courtOffset;

        public BrowserInstance(int courtOffset, string username, string password)
        {
            _courtOffset = courtOffset;
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
            while (!result)
            {
                result = TryBookCourt();
                if(result)
                {
                    result = TrySubmitCart();
                }

                Thread.Sleep(300);
            }           
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
            //courtSelector.SelectByIndex(_courtOffset);
            Console.WriteLine($"Found {courtSelector.Options.Count} courts available.");
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
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        void Login()
        {
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

            for (int i = 0; i < Options.CourtsToBook; i++)
            {
                var browser = new BrowserInstance(i, Options.UserNames[i], Options.Passwords[i]);
                _instances.Add(browser);
                browser.RunAsync();
            }

            while (UpdateInput()) { };
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
