using System;
using System.Threading;

namespace semafory
{
    class Program
    {
        static void Main(string[] args)
        {
            var semafory = new Semafory();
            semafory.run();
        }
    }

    public enum Resource
    {
        Mercury,
        Lead,
        Sulfur
    }

    public enum Alchemist
    {
        AlchemistA,
        AlchemistB,
        AlchemistC,
        AlchemistD
    }

    public class Semafory
    {
        private Random r = new Random(997);

        private int wizardCount = 1;
        private int warlockCount = 1;

        private int factoryMaxSleep = 10;
        private int wizardMaxSleep = 10;
        private int warlockMaxSleep = 10;
        private int alchemistInterval = 5;

        private const int maxResourceCount = 2;

        private Action[] alchemists;

        //czarodziej/czarownik wchodza i robia operacje na incie
        //czarodziej - dekrementacja, jezeli ==0, to signal *Cursed
        //czarownik - inkrementacja, jezeli ==1 to wait *Cursed, bez signal
        private Semaphore[] resourceCurseCountMutexes = new Semaphore[3];
        private int[] resourceCursesCount = new int[3];


        //odpowiednie fabryki czekaja na tych mutexach
        //w przypadku signala, wchodza dalej
        private Semaphore[] resourceNotCursedMutexes = new Semaphore[3];

        //SYNCHRONIZACJA alchemicy - fabryki
        //alchemicy robia signal po pobraniu resource
        //fabryki czekaja na tym
        private Semaphore[] resourceFull = new Semaphore[3];


        // Mutex kontrolujący atomowy dostęp do pierwiastków
        private Semaphore resourcesMutex = new Semaphore(1, 1);
        private int[] resourceCount = new int[3];

        private Semaphore[] alchemistMutexes = new Semaphore[4];
        private Semaphore[] alchemistCountMutexes = new Semaphore[4];
        private int[] alchemistCount = new int[4];


        public Semafory()
        {
            alchemists = new Action[]
            {
                AlchemistAWork, AlchemistBWork, AlchemistCWork, AlchemistDWork
            };

            for (int i = 0; i < 3; i++)
            {
                resourceCurseCountMutexes[i] = new Semaphore(1, 1);
                resourceNotCursedMutexes[i] = new Semaphore(1, 1);
                resourceFull[i] = new Semaphore(maxResourceCount, maxResourceCount);
            }

            for (int i = 0; i < 4; i++)
            {
                alchemistMutexes[i] = new Semaphore(0, 1);
                alchemistCountMutexes[i] = new Semaphore(1, 1);
            }
        }

        public void run()
        {
            //3 fabryki
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                FactoryWork(Resource.Lead);
            }).Start();
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                FactoryWork(Resource.Mercury);
            }).Start();
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                FactoryWork(Resource.Sulfur);
            }).Start();
            //czarodziej
            for (int i = 0; i < this.wizardCount; i++)
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    WizardWork();
                }).Start();
            //czarownik
            for (int i = 0; i < this.warlockCount; i++)
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    WarlockWork();
                }).Start();


            //alchemicy
            while (true)
            {
                Alchemist a = getRandomAlchemist();
                logEvent("Przychodzi alchemik "+ (char) ((int)a + 65) + ".");
                printAlchemics();
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    alchemists[(int)a].Invoke();
                }).Start();
                int s = r.Next(1, this.alchemistInterval);
                logEvent("Przed przyjsciem kolejnego alchemika minie " + s + " sekund.");
            
                Thread.Sleep(s * 1000);
            }
        }

        public void FactoryWork(Resource resource)
        {
            logEvent("Fabryka " + resolveResourceName(resource) + " startuje.");

            int resourceNumber = (int) resource;
            while (true)
            {
                logEvent("Fabryka " + resolveResourceName(resource) + " sprawdza czy magazyn jest pelny");
                resourceFull[resourceNumber].WaitOne();

                logEvent("Fabryka " + resolveResourceName(resource) + " czeka na zdjecie klatw");
                resourceNotCursedMutexes[resourceNumber].WaitOne();

                logEvent("Fabryka " + resolveResourceName(resource) + " nie ma klatw wiec zaczyna sie produkcja");
                int sleepTime = r.Next(1, this.factoryMaxSleep);
                logEvent("Fabryka " + resolveResourceName(resource) + " pracuje przez " + sleepTime + " sekund");
                Thread.Sleep(sleepTime * 1000);

                resourcesMutex.WaitOne();
                resourceCount[resourceNumber]++;
                logEvent("Fabryka " + resolveResourceName(resource) + " wyprodukowala towar");
                logEvent("Fabryka " + resolveResourceName(resource) + " sprawdza alchemikow");
                checkResources();
                resourcesMutex.Release();

                resourceNotCursedMutexes[resourceNumber].Release();
            }
        }

        public void WarlockWork()
        {
            //odpalany co pewien czas
            //poczekaj na mutexie od cursecount
            //zwieksz wartosc
            //jezeli == 1, wait mutex
            while (true)
            {
                Resource resource = getRandomResource();
                int resourceNumber = (int) resource;
                resourceCurseCountMutexes[resourceNumber].WaitOne();
                resourceCursesCount[resourceNumber]++;

                logEvent("Czarownik przeklina fabryke: " + resolveResourceName(resource) + "Liczba klątw: "
                         + resourceCursesCount[resourceNumber]);

                if (resourceCursesCount[resourceNumber] == 1)
                {
                    logEvent("Czarownik przeklina fabryke: " + resolveResourceName(resource) + " po raz pierwszy");
                    resourceNotCursedMutexes[resourceNumber].WaitOne();
                }

                resourceCurseCountMutexes[resourceNumber].Release();

                int sleepTime = r.Next(1, warlockMaxSleep);
                logEvent("Czarownik idzie spac na: " + sleepTime + "sekund");

                Thread.Sleep(sleepTime * 1000);
            }
        }

        public void WizardWork()
        {
            //odpalany co pewien czas
            //poczekaj na mutexie od cursecount
            //zmniejsz wartosc
            //jezeli == 0, signal mutex
            while (true)
            {
                for (int i = 0; i < 3; i++)
                {
                    Resource resource = (Resource) i;

                    resourceCurseCountMutexes[i].WaitOne();
                    int oldCurseCount = resourceCursesCount[i];
                    resourceCursesCount[i] = Math.Max(0, resourceCursesCount[i] - 1);

                    logEvent("Czarodziej zdejmuje klatwe z fabryki " + resolveResourceName(resource)
                                                                     + ", wczesniej bylo klatw: " + oldCurseCount);

                    if (oldCurseCount == 1)
                    {
                        logEvent("Czarodziej odklina fabryke " + resolveResourceName(resource));
                        resourceNotCursedMutexes[i].Release();
                    }

                    resourceCurseCountMutexes[i].Release();
                }

                int sleepTime = r.Next(1, this.wizardMaxSleep);
                logEvent("Czarodziej idzie spac na " + sleepTime + " sekund");
                Thread.Sleep(sleepTime * 1000);
            }
        }

        public void AlchemistAWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistA;
            changeAlchemistNumber(Alchemist.AlchemistA, 1);
            logEvent("Alchemik A przyszedl, sprawdza czy ma dostepne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistA))
            {
                logEvent("Alchemik A odchodzi - mial wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistA, -1);
                return;
            }

            alchemistMutexes[alchemistNumber].WaitOne();
            checkResourcesAlchemist(Alchemist.AlchemistA);
            changeAlchemistNumber(Alchemist.AlchemistA, -1);
            logEvent("Alchemik A został obsluzony przez fabryke");
        }

        public void AlchemistBWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistB;
            changeAlchemistNumber(Alchemist.AlchemistB, 1);
            logEvent("Alchemik B przyszedl, sprawdza czy ma dostepne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistB))
            {
                logEvent("Alchemik B odchodzi - mial wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistB, -1);
                return;
            }

            alchemistMutexes[alchemistNumber].WaitOne();
            checkResourcesAlchemist(Alchemist.AlchemistB);
            changeAlchemistNumber(Alchemist.AlchemistB, -1);
            logEvent("Alchemik B został obsluzony przez fabryke");
        }

        public void AlchemistCWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistC;
            changeAlchemistNumber(Alchemist.AlchemistC, 1);
            logEvent("Alchemik C przyszedl, sprawdza czy ma dostepne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistC))
            {
                logEvent("Alchemik C odchodzi - mial wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistC, -1);
                return;
            }

            alchemistMutexes[alchemistNumber].WaitOne();
            checkResourcesAlchemist(Alchemist.AlchemistC);
            changeAlchemistNumber(Alchemist.AlchemistC, -1);
            logEvent("Alchemik C został obsluzony przez fabryke");
        }

        public void AlchemistDWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistD;
            changeAlchemistNumber(Alchemist.AlchemistD, 1);
            logEvent("Alchemik D przyszedl, sprawdza czy ma dostepne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistD))
            {
                logEvent("Alchemik D odchodzi - mial wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistD, -1);
                return;
            }

            alchemistMutexes[alchemistNumber].WaitOne();
            checkResourcesAlchemist(Alchemist.AlchemistD);
            changeAlchemistNumber(Alchemist.AlchemistD, -1);
            logEvent("Alchemik D został obsluzony przez fabryke");
        }

        private void checkResources()
        {
            bool isSulfur = resourceCount[(int) Resource.Sulfur] > 0;
            bool isLead = resourceCount[(int) Resource.Lead] > 0;
            bool isMercury = resourceCount[(int) Resource.Mercury] > 0;


            if (isSulfur && isLead && isMercury)
            {
                //D
                int alchemistType = (int) Alchemist.AlchemistD;
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                }
            }

            if (isSulfur && isMercury)
            {
                //B
                int alchemistType = (int) Alchemist.AlchemistB;
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                }
            }

            if (isMercury && isLead)
            {
                //A
                int alchemistType = (int) Alchemist.AlchemistA;
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                }
            }

            if (isLead && isSulfur)
            {
                //C
                int alchemistType = (int) Alchemist.AlchemistC;
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                }
            }
        }

        private bool checkResourcesAlchemist(Alchemist alchemist)
        {
            resourcesMutex.WaitOne();
            bool isSulfur = resourceCount[(int) Resource.Sulfur] > 0;
            bool isLead = resourceCount[(int) Resource.Lead] > 0;
            bool isMercury = resourceCount[(int) Resource.Mercury] > 0;

            if (resourceCount[(int) Resource.Sulfur] < 0 || resourceCount[(int) Resource.Lead] < 0 ||
                resourceCount[(int) Resource.Mercury] < 0)
                throw new Exception("Fabryka sprawdzila, zasoby < 0 !");

            int mercuryNumber = (int) Resource.Mercury;
            int leadNumber = (int) Resource.Lead;
            int sulfurNumber = (int) Resource.Sulfur;

            if (alchemist == Alchemist.AlchemistA && isMercury && isLead)
            {
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
                resourcesMutex.Release();
                return true;
            }

            if (alchemist == Alchemist.AlchemistB && isSulfur && isMercury)
            {
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
                resourcesMutex.Release();
                return true;
            }

            if (alchemist == Alchemist.AlchemistC && isLead && isSulfur)
            {
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
                resourcesMutex.Release();
                return true;
            }

            if (alchemist == Alchemist.AlchemistC && isLead && isSulfur && isMercury)
            {
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
                resourcesMutex.Release();
                return true;
            }

            resourcesMutex.Release();
            return false;
        }

        private void changeAlchemistNumber(Alchemist alchemist, int addNumber)
        {
            int alchemistNumber = (int) alchemist;
            alchemistCountMutexes[alchemistNumber].WaitOne();
            alchemistCount[alchemistNumber] += addNumber;
            alchemistCountMutexes[alchemistNumber].Release();
        }

        private Resource getRandomResource()
        {
            Array values = Enum.GetValues(typeof(Resource));
            return (Resource) values.GetValue(r.Next(values.Length));
        }
        
        private Alchemist getRandomAlchemist()
        {
            Array values = Enum.GetValues(typeof(Alchemist));
            return (Alchemist) values.GetValue(r.Next(values.Length));
        }

        private void logEvent(String eventText)
        {
            System.Console.WriteLine(System.DateTime.Now + " " + eventText);
        }

        private String resolveResourceName(Resource resource)
        {
            if (resource == Resource.Sulfur)
                return "\"siarka\"";
            else if (resource == Resource.Lead)
                return "\"olow\"";

            return "\"rtec\"";
        }

        private void printAlchemics()
        {
            System.Console.WriteLine(System.DateTime.Now + " OCZEKUJACY ALCHEMICY:");
            System.Console.WriteLine("Alchemicy A - " + alchemistCount[(int) Alchemist.AlchemistA]);
            System.Console.WriteLine("Alchemicy B - " + alchemistCount[(int) Alchemist.AlchemistB]);
            System.Console.WriteLine("Alchemicy C - " + alchemistCount[(int) Alchemist.AlchemistC]);
            System.Console.WriteLine("Alchemicy D - " + alchemistCount[(int) Alchemist.AlchemistD]);
            System.Console.WriteLine(System.DateTime.Now + " ZASOBY:");
            System.Console.WriteLine("Rtec - " + resourceCount[(int) Resource.Mercury]);
            System.Console.WriteLine("Siarka - " + resourceCount[(int) Resource.Sulfur]);
            System.Console.WriteLine("Olow - " + resourceCount[(int) Resource.Lead]);
        }
    }
}