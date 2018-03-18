using System;
using System.Collections.Generic;
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
        private const int maxResourceCount = 2;

        private int wizardCount = 2;
        private int warlockCount = 3;

        //edytowalne zmienne od spania
        private double[] factorySleepRange = new double[] {1,2};
        private double[] wizardSleepRange = new double[] {1,2};
        private double[] warlockSleepRange = new double[] {1,2};
        private double[] alchemistAppearRange = new double[] {0.5,1.8};

        private Action[] alchemists;

        //dostep do liczb klatw dla kazdego zasobu:
        private Semaphore[] resourceCurseCountMutexes = new Semaphore[3];
        private int[] resourceCursesCount = new int[3];


        //mutexy blokowane w przypadku obecnosci klatw:
        private Semaphore[] resourceNotCursedMutexes = new Semaphore[3];

        //fabryki czekaja na tych semaforach gdy magazyny sa pelne:
        private Semaphore[] resourceFull = new Semaphore[3];


        //mutex kontrolujacy dostep do magazynów:
        private Semaphore resourcesMutex = new Semaphore(1, 1);
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

                resourcesMutex.WaitOne();
                resourceCount[resourceNumber]++;
                logEvent("Fabryka " + resolveResourceName(resource) + " wyprodukowała towar i sprawdza kolejkę alchemików");
                checkAndSignalAlchemics(resource);
                resourcesMutex.Release();

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

        public void AlchemistAWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistA;
            changeAlchemistNumber(Alchemist.AlchemistA, 1);
            logEvent("Alchemik A przyszedł, sprawdza czy ma dostępne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistA))
            {
                logEvent("Alchemik A odchodzi - wziął wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistA, -1);
                return;
            }
            logEvent("Alchemik A nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistA, -1);
        }

        public void AlchemistBWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistB;
            changeAlchemistNumber(Alchemist.AlchemistB, 1);
            logEvent("Alchemik B przyszedł, sprawdza czy ma dostępne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistB))
            {
                logEvent("Alchemik B odchodzi - wziął wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistB, -1);
                return;
            }

            logEvent("Alchemik B nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistB, -1);
        }

        public void AlchemistCWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistC;
            changeAlchemistNumber(Alchemist.AlchemistC, 1);
            logEvent("Alchemik C przyszedł, sprawdza czy ma dostępne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistC))
            {
                logEvent("Alchemik C odchodzi - wziął wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistC, -1);
                return;
            }

            logEvent("Alchemik C nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistC, -1);
        }

        public void AlchemistDWork()
        {
            int alchemistNumber = (int) Alchemist.AlchemistD;
            changeAlchemistNumber(Alchemist.AlchemistD, 1);
            logEvent("Alchemik D przyszedł, sprawdza czy ma dostępne zasoby");

            if (checkResourcesAlchemist(Alchemist.AlchemistD))
            {
                logEvent("Alchemik D odchodzi - wziął wszystkie zasoby");
                changeAlchemistNumber(Alchemist.AlchemistD, -1);
                return;
            }

            logEvent("Alchemik D nie ma zasobów - oczekuje na obsługę");
            alchemistMutexes[alchemistNumber].WaitOne();
            changeAlchemistNumber(Alchemist.AlchemistD, -1);
        }

        private List<Resource>[] neededResourcesByAlchemist = new List<Resource>[]
        {
            new List<Resource> {Resource.Mercury, Resource.Lead}, //A
            new List<Resource> {Resource.Sulfur, Resource.Mercury}, //B
            new List<Resource> {Resource.Lead, Resource.Sulfur}, //C
            new List<Resource> {Resource.Lead, Resource.Mercury, Resource.Sulfur} //D
        };
        
        private bool CheckAvailabilityForAlchemist(Alchemist alchemist, bool isSulfur, bool isLead, bool isMercury)
        {
            bool ret = true;

            foreach(var res in neededResourcesByAlchemist[(int)alchemist])
            {
                if (res == Resource.Sulfur)
                    ret = ret && isSulfur;
                if (res == Resource.Lead)
                    ret = ret && isLead;
                if (res == Resource.Mercury)
                    ret = ret && isMercury;
            }

            return ret;
        }
        private void CheckAvailabilityAndGiveResources(Alchemist alchemist, bool isSulfur, bool isLead, bool isMercury, Resource factoryType)
        {
            if(CheckAvailabilityForAlchemist(alchemist, isSulfur, isLead, isMercury))
            {
                int alchemistType = (int)alchemist;
                if (alchemistCount[alchemistType] > 0)
                {
                    alchemistMutexes[alchemistType].Release();
                    logEvent("Fabryka " + resolveResourceName(factoryType) + " wybiera zasoby dla alchemika" + alchemist.ToString());
                    acquireResources(alchemist);
                }
            }
        }

        //procedura wybierania alchemika z kolejki i dawania mu zasobów, uruchamiana przez fabrykę po wyprodukowaniu surowca
        //uruchamiana wewnątrz mutexu resourcesMutex!
        private void checkAndSignalAlchemics(Resource resource)
        {
            bool isSulfur = resourceCount[(int) Resource.Sulfur] > 0;
            bool isLead = resourceCount[(int) Resource.Lead] > 0;
            bool isMercury = resourceCount[(int) Resource.Mercury] > 0;

            if (resourceCount[(int) Resource.Sulfur] < 0 || resourceCount[(int) Resource.Lead] < 0 ||
                resourceCount[(int) Resource.Mercury] < 0)
                throw new Exception("Zasoby < 0 !");

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
                CheckAvailabilityAndGiveResources((Alchemist)currentAlchemistCounts[i].Item1, isSulfur, isLead, isMercury, resource);
                isSulfur = resourceCount[(int)Resource.Sulfur] > 0;
                isLead = resourceCount[(int)Resource.Lead] > 0;
                isMercury = resourceCount[(int)Resource.Mercury] > 0;
            }
        }

        //wybieranie zasobów z fabryk przez alchemika
        private bool checkResourcesAlchemist(Alchemist alchemist)
        {
            resourcesMutex.WaitOne();
            bool isSulfur = resourceCount[(int) Resource.Sulfur] > 0;
            bool isLead = resourceCount[(int) Resource.Lead] > 0;
            bool isMercury = resourceCount[(int) Resource.Mercury] > 0;

            if (resourceCount[(int) Resource.Sulfur] < 0 || resourceCount[(int) Resource.Lead] < 0 ||
                resourceCount[(int) Resource.Mercury] < 0)
                throw new Exception("Zasoby < 0 !");

            if ((alchemist == Alchemist.AlchemistA && isMercury && isLead)   ||
                (alchemist == Alchemist.AlchemistB && isSulfur && isMercury) ||
                (alchemist == Alchemist.AlchemistC && isLead && isSulfur)    ||
                (alchemist == Alchemist.AlchemistD && isLead && isSulfur && isMercury))
            {
                acquireResources(alchemist);
                resourcesMutex.Release();
                return true;
            }

            resourcesMutex.Release();
            return false;
        }
        
        //rzeczywiste operacje pozyskania zasobów z magazynu - wymaga bycia w mutexie resourcesMutex!
        private void acquireResources (Alchemist alchemist)
        {
            int mercuryNumber = (int) Resource.Mercury;
            int leadNumber = (int) Resource.Lead;
            int sulfurNumber = (int) Resource.Sulfur;

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
    }
}