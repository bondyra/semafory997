using System;
using System.Threading;
using System.Collections.Generic;

namespace jb_semafory
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
        private const int maxResourceCount = 2;
        
        private int mercuryNumber = (int) Resource.Mercury;
        private int leadNumber = (int) Resource.Lead;
        private int sulfurNumber = (int) Resource.Sulfur;

        private int wizardCount = 2;
        private int warlockCount = 3;

        //edytowalne zmienne od spania
        private double[] factorySleepRange = new double[] {0.3,0.8};
        private double[] wizardSleepRange = new double[] {0.3,0.8};
        private double[] warlockSleepRange = new double[] {0.3,0.8};
        private double[] alchemistAppearRange = new double[] {0.2,0.9};

        private Action[] alchemists;

        //dostep do liczb klatw dla kazdego zasobu:
        private Semaphore[] resourceCurseCountMutexes = new Semaphore[3];
        private int[] resourceCursesCount = new int[3];


        //mutexy blokowane w przypadku obecnosci klatw:
        private Semaphore[] resourceNotCursedMutexes = new Semaphore[3];

        //fabryki czekaja na tych semaforach gdy magazyny sa pelne:
        private Semaphore[] resourceFull = new Semaphore[3];


        //mutex kontrolujacy dostep do magazynów:
        private Semaphore[] resourceMutexes = new Semaphore[3];
        private int[] resourceCount = new int[3];

        //mutexy, na ktorych czekaja alchemicy poszczegolnych grup (oczekiwanie na zasoby):
        private Semaphore[] alchemistMutexes = new Semaphore[4];

        //mutexy gwarantujace niepodzielny dostep do licznikow oczekujacych alchemikow:
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
                resourceMutexes[i] = new Semaphore(1,1);
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

            //czarodzieje
            for (int i = 0; i < this.wizardCount; i++)
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    WizardWork(i);
                }).Start();

            //czarownicy
            for (int i = 0; i < this.warlockCount; i++)
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    WarlockWork(i);
                }).Start();


            //alchemicy
            while (true)
            {
                printValues();
                Alchemist a = getRandomAlchemist();
                logEvent("Przychodzi alchemik "+ (char) ((int)a + 65));
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    alchemists[(int)a].Invoke();
                }).Start();
                int sleepTime = getRandomMiliseconds(alchemistAppearRange[0], alchemistAppearRange[1]);
                logEvent("Sekund do przyjścia kolejnego alchemika: " + Math.Round(sleepTime/1000f, 2));
            
                Thread.Sleep(sleepTime);
            }
        }

        public void FactoryWork(Resource resource)
        {
            logEvent("Fabryka " + resolveResourceName(resource) + " startuje.");

            int resourceNumber = (int) resource;
            while (true)
            {
                logEvent("Fabryka " + resolveResourceName(resource) + " sprawdza czy magazyn jest pełny");
                resourceFull[resourceNumber].WaitOne();

                logEvent("Fabryka " + resolveResourceName(resource) + " czeka na zdjęcie klatw");
                resourceNotCursedMutexes[resourceNumber].WaitOne();

                logEvent("Fabryka " + resolveResourceName(resource) + " nie ma teraz klątw wiec zaczyna się produkcja");
                int sleepTime = getRandomMiliseconds(factorySleepRange[0], factorySleepRange[1]);
                logEvent("Fabryka " + resolveResourceName(resource) + " pracuje przez " + Math.Round(sleepTime/1000f,2) + " sekund");
                Thread.Sleep(sleepTime);

                resourceMutexes[resourceNumber].WaitOne();
                resourceCount[resourceNumber]++;
                resourceMutexes[resourceNumber].Release();
                logEvent("Fabryka " + resolveResourceName(resource) + " wyprodukowała towar i sprawdza kolejkę alchemików");
                checkAndSignalAlchemists(resource);

                resourceNotCursedMutexes[resourceNumber].Release();
            }
        }

        public void WarlockWork(int id)
        {
            while (true)
            {
                Resource resource = getRandomResource();
                int resourceNumber = (int) resource;
                
                resourceCurseCountMutexes[resourceNumber].WaitOne();
                int oldResourceCurseCount = resourceCursesCount[resourceNumber];
                resourceCursesCount[resourceNumber]++;

                logEvent("Czarownik "+id+" przeklina fabrykę: " + resolveResourceName(resource) + ", obecna liczba klątw: "
                         + resourceCursesCount[resourceNumber]);

                resourceCurseCountMutexes[resourceNumber].Release();

                //czarownik poczeka na ew. koniec pracy fabryki i zablokuje dostęp do produkcji:
                if (oldResourceCurseCount == 0)
                    resourceNotCursedMutexes[resourceNumber].WaitOne();

                int sleepTime = getRandomMiliseconds(warlockSleepRange[0], warlockSleepRange[1]);
                logEvent("Sekund snu czarownika "+id+": " + Math.Round(sleepTime/1000f,2));

                Thread.Sleep(sleepTime);
            }
        }

        public void WizardWork(int id)
        {
            while (true)
            {
                for (int i = 0; i < 3; i++)
                {
                    Resource resource = (Resource) i;

                    resourceCurseCountMutexes[i].WaitOne();
                    int oldCurseCount = resourceCursesCount[i];
                    resourceCursesCount[i] = Math.Max(0, resourceCursesCount[i] - 1);
                    if (oldCurseCount > 0)
                        logEvent("Czarodziej "+id+" zdejmuje klatwę z fabryki " + resolveResourceName(resource)
                                                                     + ", obecna liczba klątw: " + resourceCursesCount[i]);

                    resourceCurseCountMutexes[i].Release();

                    if (oldCurseCount == 1)
                        resourceNotCursedMutexes[i].Release();
                }

                int sleepTime = getRandomMiliseconds(this.wizardSleepRange[0], this.wizardSleepRange[1]);
                logEvent("Sekund snu czarodzieja "+id+": " + Math.Round(sleepTime/1000f,2));
                Thread.Sleep(sleepTime);
            }
        }
	
	//praca alchemikow - 4 funkcje
        public void AlchemistAWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistA;
            logEvent("Alchemik A przyszedł, sprawdza czy ma dostępne zasoby (ołów, rtęć)");
            if (alchemistGetsResources(Alchemist.AlchemistA))
            {
                logEvent("Alchemik A odchodzi - wziął wszystkie zasoby");
                return;
            }
            
            changeAlchemistNumber(Alchemist.AlchemistA, 1);
            logEvent("Alchemik A nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
		//note: zabieranie zasobow nalezy do fabryki
            changeAlchemistNumber(Alchemist.AlchemistA, -1);
        }

        public void AlchemistBWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistB;
            logEvent("Alchemik B przyszedł, sprawdza czy ma dostępne zasoby (siarka, rtęć)");

            if (alchemistGetsResources(Alchemist.AlchemistB))
            {
                logEvent("Alchemik B odchodzi - wziął wszystkie zasoby");
                return;
            }
            
            changeAlchemistNumber(Alchemist.AlchemistB, 1);
            logEvent("Alchemik B nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistB, -1);
        }

        public void AlchemistCWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistC;
            logEvent("Alchemik C przyszedł, sprawdza czy ma dostępne zasoby (siarka, ołów)");

            if (alchemistGetsResources(Alchemist.AlchemistC))
            {
                logEvent("Alchemik C odchodzi - wziął wszystkie zasoby");
                return;
            }

            changeAlchemistNumber(Alchemist.AlchemistC, 1);
            logEvent("Alchemik C nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistC, -1);
        }

        public void AlchemistDWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistD;
            logEvent("Alchemik D przyszedł, sprawdza czy ma dostępne zasoby (wszystkie)");

            if (alchemistGetsResources(Alchemist.AlchemistD))
            {
                logEvent("Alchemik D odchodzi - wziął wszystkie zasoby");
                return;
            }

            changeAlchemistNumber(Alchemist.AlchemistD, 1);
            logEvent("Alchemik D nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistD, -1);
        }

	//zabieranie zasobow w imieniu alchemika (odpalane z fabryki)
        private void giveResources(Alchemist alchemist, Resource factoryType)
        {
            waitProperResourceMutexes(alchemist);
            if (checkResources(alchemist))
            {
                int alchemistType = (int)alchemist;

                alchemistCountMutexes[alchemistType].WaitOne();
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                    logEvent("Fabryka " + resolveResourceName(factoryType) + " wybiera zasoby dla alchemika " + (char) ((int)alchemist + 65));
                    acquireResources(alchemist); 
                }
                alchemistCountMutexes[alchemistType].Release();
            }

            releaseProperResourceMutexes(alchemist);
        }

        //procedura wybierania alchemika z kolejki i dawania mu zasobów, uruchamiana przez fabrykę po wyprodukowaniu surowca
        private void checkAndSignalAlchemists(Resource resource)
        {
            //priorytetyzacja kolejek alchemikow
            Tuple<int, int>[] currentAlchemistCounts = new Tuple<int, int>[4]; // tablica alchemist, alchemistcount

            for(int i =0; i<alchemistCount.Length; i++)
            {
                alchemistCountMutexes[i].WaitOne();
                currentAlchemistCounts[i] = new Tuple<int, int>(i, alchemistCount[i]);
                alchemistCountMutexes[i].Release();
            }

            Array.Sort(currentAlchemistCounts, (x, y) => y.Item2.CompareTo(x.Item2));


            for(int i=0; i< currentAlchemistCounts.Length; i++)
            {
                giveResources((Alchemist)currentAlchemistCounts[i].Item1, resource);
            }
        }

        //wybieranie zasobów z fabryk przez alchemika
        private bool alchemistGetsResources(Alchemist alchemist)
        {
            waitProperResourceMutexes(alchemist);

            if (checkResources(alchemist))
            {
                acquireResources(alchemist);
                releaseProperResourceMutexes(alchemist);
                return true;
            }

            releaseProperResourceMutexes(alchemist);
            return false;
        }
        
        //rzeczywiste operacje pozyskania zasobów z magazynu - wymaga bycia w odpowiednich mutexach resourceMutexes!
        private void acquireResources (Alchemist alchemist)
        {
            if (alchemist == Alchemist.AlchemistA)
            {
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
            }

            if (alchemist == Alchemist.AlchemistB)
            {
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
            }

            if (alchemist == Alchemist.AlchemistC)
            {
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
            }

            if (alchemist == Alchemist.AlchemistD)
            {
                resourceCount[leadNumber]--;
                resourceFull[leadNumber].Release();
                resourceCount[sulfurNumber]--;
                resourceFull[sulfurNumber].Release();
                resourceCount[mercuryNumber]--;
                resourceFull[mercuryNumber].Release();
            }
        }

        //sprawdzenie czy sa zasoby - wymaga bycia w odpowiednich mutexach!
        private bool checkResources (Alchemist alchemist)
        {
            if (alchemist == Alchemist.AlchemistA)
            {
                return (resourceCount[mercuryNumber] > 0 && resourceCount[leadNumber] > 0);
            }            
            if (alchemist == Alchemist.AlchemistB)
            {
                return (resourceCount[mercuryNumber] > 0 && resourceCount[sulfurNumber] > 0);
            }
            if (alchemist == Alchemist.AlchemistC)
            {
                return (resourceCount[leadNumber] > 0 && resourceCount[sulfurNumber] > 0);
            }
            if (alchemist == Alchemist.AlchemistD)
            {
                return (resourceCount[leadNumber] > 0 && resourceCount[sulfurNumber] > 0 && resourceCount[mercuryNumber] > 0);
            }
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
                return "\"ołów\"";

            return "\"rtęć\"";
        }

        private void printValues()
        {
		//print bez bycia w mutexach - bo nie ma wiekszej potrzeby
            System.Console.WriteLine("OCZEKUJĄCY ALCHEMICY:");
            System.Console.WriteLine("A - " + alchemistCount[(int) Alchemist.AlchemistA]);
            System.Console.WriteLine("B - " + alchemistCount[(int) Alchemist.AlchemistB]);
            System.Console.WriteLine("C - " + alchemistCount[(int) Alchemist.AlchemistC]);
            System.Console.WriteLine("D - " + alchemistCount[(int) Alchemist.AlchemistD]);
            System.Console.WriteLine("OBECNE ZASOBY:");
            System.Console.WriteLine("Rtęć - " + resourceCount[(int) Resource.Mercury]);
            System.Console.WriteLine("Siarka - " + resourceCount[(int) Resource.Sulfur]);
            System.Console.WriteLine("Ołów - " + resourceCount[(int) Resource.Lead]);
        }

        private int getRandomMiliseconds(double min, double max){
            double m = r.NextDouble();
            return (int)(1000*(min+m*(max-min)));
        }

        private void waitProperResourceMutexes(Alchemist a)
        {
            if (a == Alchemist.AlchemistA)
            {
                resourceMutexes[mercuryNumber].WaitOne();
                resourceMutexes[leadNumber].WaitOne();
            }            
            if (a == Alchemist.AlchemistB)
            {
                resourceMutexes[sulfurNumber].WaitOne();
                resourceMutexes[mercuryNumber].WaitOne();
            }
            if (a == Alchemist.AlchemistC)
            {
                resourceMutexes[sulfurNumber].WaitOne();
                resourceMutexes[leadNumber].WaitOne();
            }
            if (a == Alchemist.AlchemistD)
            {
                resourceMutexes[sulfurNumber].WaitOne();
                resourceMutexes[mercuryNumber].WaitOne();
                resourceMutexes[leadNumber].WaitOne();
            }
        }

        private void releaseProperResourceMutexes(Alchemist a)
        {
            if (a == Alchemist.AlchemistA)
            {
                resourceMutexes[mercuryNumber].Release();
                resourceMutexes[leadNumber].Release();
            }            
            if (a == Alchemist.AlchemistB)
            {
                resourceMutexes[sulfurNumber].Release();
                resourceMutexes[mercuryNumber].Release();
            }
            if (a == Alchemist.AlchemistC)
            {
                resourceMutexes[sulfurNumber].Release();
                resourceMutexes[leadNumber].Release();
            }
            if (a == Alchemist.AlchemistD)
            {
                resourceMutexes[sulfurNumber].Release();
                resourceMutexes[mercuryNumber].Release();
                resourceMutexes[leadNumber].Release();
            }
        }
    }
}
