using CommandLine;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Threading;

namespace UltimateBadmintonBooker
{
    class Options
    {
        [Option('d', "BookingDate", Required = true, HelpText = "In the format of 19_11_2019.")]
        public string BookingDate { get; set; }
        [Option('t', "StartTime", Required = true, HelpText = "In the format of 6_00 or 20_00.")]
        public string StartTime { get; set; }
        [Option('u', "UserName", Required = true, HelpText = "Username login.")]
        public string UserName { get; set; }
        [Option('p', "Password", Required = true, HelpText = "Login password.")]
        public string Password { get; set; }
    }

    class Program
    {
        static Options _options;
        static IWebDriver _driver;

        static int courtsToBook = 2;
        static int _bookingAttempts;

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => Initialize(opts))
                .WithNotParsed((errs) => HandleParseError(errs));

            RunBabyRun();

            while (true) {
                UpdateInput();
            };
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
            _options = opts;
        }

        public static void RunBabyRun()
        {
            _driver = new ChromeDriver(AppDomain.CurrentDomain.BaseDirectory);

            Login();

            int courtsBooked = 0;

            while (courtsBooked < courtsToBook)
            {
                UpdateInput();

                var result = TryBookCourt();
                if (result) courtsBooked++;

                _bookingAttempts++;
                Thread.Sleep(300);
            }

            SubmitCart();
        }

        static void UpdateInput()
        {
            if(Console.ReadKey().Key == ConsoleKey.Escape)
            {
                throw new Exception("Quit!");
            }
        }

        static bool TryBookCourt()
        {
            try
            {
                if (TryGetBookingTime()) TryAddCourtToCart();
                else return false;
            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        static void TryAddCourtToCart()
        {
            var addToBasketBtn = _driver.FindElement(By.XPath(".//*[@id='basketControl_1_1']"));            
            if(addToBasketBtn.GetAttribute("class").Contains("sr_TooEarly"))
            {
                throw new Exception("Too early to book courts.");                
            }

            addToBasketBtn.Click();
            var courtSelector = new SelectElement(_driver.FindElement(By.XPath(".//*[@id='subLocSelect']")));
            Console.WriteLine($"Found {courtSelector.Options.Count} courts available.");
            var submitCourtBtn = _driver.FindElement(By.XPath("//span[text()='OK']"));
            submitCourtBtn.Click();            
        }

        static void SubmitCart()
        {
            var termsToggle = _driver.FindElement(By.XPath(".//*[@id='TermsAccepted']"));
            termsToggle.Click();
            termsToggle.Submit();
        }

        static void Login()
        {
            _driver.Url = LoginUrl();
            var usernameField = _driver.FindElement(By.XPath(".//*[@id='UserName']"));
            var passwordField = _driver.FindElement(By.XPath(".//*[@id='Password']"));
            usernameField.SendKeys(_options.UserName);
            passwordField.SendKeys(_options.Password);
            passwordField.Submit();
        }

        static bool TryGetBookingTime()
        {
            _driver.Url = SearchPageUrl(_options.BookingDate, _options.StartTime, "22_00");
            try
            {
                _driver.FindElement(By.XPath(".//*[@id='NoSearchResultsNotice']"));
            } catch (Exception)
            {
                try
                {
                    while (_driver.FindElement(By.XPath(".//*[text()='Retrieving']")) != null) Thread.Sleep(100);
                } catch(Exception)
                {
                    return true;
                }
                return true;
            }

            return false;
        }

        static string SearchPageUrl(string date, string startTime, string endTime)
        {
            return $"https://bookwkg.freedom-leisure.co.uk/kingalfredbookings/Search/PerformSearch?SiteID=1&Activity=0111&EnglishDate={date}&English_TimeFrom={startTime}&English_TimeTo={endTime}&Duration=120&submitButton=Search";
        }

        static string LoginUrl()
        {
            return $"https://bookwkg.freedom-leisure.co.uk/kingalfredbookings/Account/LogOn";
        }
    }
}
