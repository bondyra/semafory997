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

    public class Semafory {
        private Random r = new Random(997);
        private const int lead = 2;
        private const int mercury = 1;
        private const int sulfur = 3;

        private int wizardCount = 1;
        private int warlockCount = 1;

        private int factoryMaxSleep = 10;
        private int wizardMaxSleep = 10;
        private int warlockMaxSleep = 10;
        private int alchemistInterval = 5;

        private Action[] alchemists;


        //czarodziej/czarownik wchodza i robia operacje na incie
        //czarodziej - dekrementacja, jezeli ==0, to signal *Cursed
        //czarownik - inkrementacja, jezeli ==1 to wait *Cursed, bez signal
        private Semaphore mercuryCurseCountMutex = new Semaphore(1,1);
        private Semaphore leadCurseCountMutex = new Semaphore(1,1);
        private Semaphore sulfurCurseCountMutex = new Semaphore(1,1);
        private int mercuryCurseCount = 0;
        private int leadCurseCount = 0;
        private int sulfurCurseCount = 0;


        //odpowiednie fabryki czekaja na tych mutexach
        //w przypadku signala, wchodza dalej
        private Semaphore mercuryNotCursedMutex = new Semaphore(1,1);
        private Semaphore leadNotCursedMutex = new Semaphore(1,1);
        private Semaphore sulfurNotCursedMutex = new Semaphore(1,1);


        //SYNCHRONIZACJA alchemicy - fabryki
        //alchemicy robia signal po pobraniu resource
        //fabryki czekaja na tym
        private Semaphore mercuryFull = new Semaphore(2,2);
        private Semaphore leadFull = new Semaphore(2,2);
        private Semaphore sulfurFull = new Semaphore(2,2);

        //fabryki po wyprodukowaniu robia signal
        //alchemicy czekaja na tym
        private Semaphore mercuryEmpty = new Semaphore(0,2);
        private Semaphore leadEmpty = new Semaphore(0,2);
        private Semaphore sulfurEmpty = new Semaphore(0,2);

        private Semaphore resourcesMutex = new Semaphore(1,1);
        private int mercuryCount = 0;
        private int leadCount = 0;
        private int sulfurCount = 0;


        private Semaphore alchemistAMutex = new Semaphore(0,1);
        private Semaphore alchemistBMutex = new Semaphore(0,1);
        private Semaphore alchemistCMutex = new Semaphore(0,1);
        private Semaphore alchemistDMutex = new Semaphore(0,1);

        private Semaphore alchemistACountMutex = new Semaphore(1,1);
        private Semaphore alchemistBCountMutex = new Semaphore(1,1);
        private Semaphore alchemistCCountMutex = new Semaphore(1,1);
        private Semaphore alchemistDCountMutex = new Semaphore(1,1);
        private int alchemistACount = 0;
        private int alchemistBCount = 0;
        private int alchemistCCount = 0;
        private int alchemistDCount = 0;

        public void run(){
            //3 fabryki
            new Thread(()=> {Thread.CurrentThread.IsBackground = true; FactoryWork(lead);}).Start();
            new Thread(()=> {Thread.CurrentThread.IsBackground = true; FactoryWork(mercury);}).Start();
            new Thread(()=> {Thread.CurrentThread.IsBackground = true; FactoryWork(sulfur);}).Start();
            //czarodziej
            for (int i=0;i<this.wizardCount;i++)
                new Thread(()=> {Thread.CurrentThread.IsBackground = true; WizardWork();}).Start();
            //czarownik
            for (int i=0;i<this.warlockCount;i++)
                new Thread(()=> {Thread.CurrentThread.IsBackground = true; WarlockWork();}).Start();
            //alchemicy
            while(true){
                int a = r.Next(0,4);
                System.Console.WriteLine(System.DateTime.Now+" "+"Przychodzi alchemik "+(char)(a+65)+".");
                printAlchemics();
                new Thread(() => {Thread.CurrentThread.IsBackground = true; alchemists[a].Invoke();}).Start();
                int s = r.Next(1,this.alchemistInterval);
                System.Console.WriteLine(System.DateTime.Now+" "+"Przed przyjsciem kolejnego alchemika minie "+s+" sekund.");
                Thread.Sleep(s*1000);
            }
        }

        public Semafory()
        {
            this.alchemists = new Action[] {
            AlchemistAWork, AlchemistBWork, AlchemistCWork, AlchemistDWork
            };
        }

        public void FactoryWork (int resource){
            //w wiecznej petli powtarzaj:
            System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" startuje.");
            while(true){
                if (resource==mercury){                    
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" sprawdza czy magazyn jest pelny.");
                    mercuryFull.WaitOne();

                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" czeka na zdjecie klatw.");
                    mercuryNotCursedMutex.WaitOne();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Dla fabryki "+resolveResourceName(resource)+" nie ma klatw, zaczyna sie produkcja.");
                    int s = r.Next(1,this.factoryMaxSleep);
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" pracuje przez "+s+" sekund.");
                    Thread.Sleep(s*1000);
                    resourcesMutex.WaitOne();
                    mercuryCount++;
                    //fabryka sprawdza alchemikow
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka"+resolveResourceName(resource)+" sprawdza alchemikow.");
                    checkResources();
                    resourcesMutex.Release();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" wyprodukowala towar.");
                    mercuryNotCursedMutex.Release();
                    //mercuryEmpty.Release();
                }
                else if (resource == lead){                    
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" sprawdza czy magazyn jest pelny.");
                    leadFull.WaitOne();

                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" czeka na zdjecie klatw.");
                    leadNotCursedMutex.WaitOne();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Dla fabryki "+resolveResourceName(resource)+" nie ma klatw, zaczyna sie produkcja.");
                    int s = r.Next(1,this.factoryMaxSleep);
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" pracuje przez "+s+" sekund.");
                    Thread.Sleep(s*1000);
                    resourcesMutex.WaitOne();
                    leadCount++;
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka"+resolveResourceName(resource)+" sprawdza alchemikow.");
                    checkResources();
                    resourcesMutex.Release();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" wyprodukowala towar.");
                    leadNotCursedMutex.Release();
                    //leadEmpty.Release();
                }
                else{
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" sprawdza czy magazyn jest pelny.");
                    sulfurFull.WaitOne();

                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" czeka na zdjecie klatw.");
                    sulfurNotCursedMutex.WaitOne();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Dla fabryki "+resolveResourceName(resource)+" nie ma klatw, zaczyna sie produkcja.");
                    int s = r.Next(1,this.factoryMaxSleep);
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" pracuje przez "+s+" sekund.");
                    Thread.Sleep(s*1000);
                    resourcesMutex.WaitOne();
                    sulfurCount++;
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka"+resolveResourceName(resource)+" sprawdza alchemikow.");
                    checkResources();
                    resourcesMutex.Release();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka "+resolveResourceName(resource)+" wyprodukowala towar.");
                    sulfurNotCursedMutex.Release();
                    //sulfurEmpty.Release();
                }
            }
            //poczekaj na mutexie *NotCursedMutex
            //poczekaj na semaforze *Full
            //spij pewien czas (fabryka pracuje)
            //zasygnalizuj semafor *Empty
        }

        private void checkResources (){
            bool isSulfur = sulfurCount>0;
            bool isLead = leadCount>0;
            bool isMercury = mercuryCount>0;

            if (leadCount<0 || sulfurCount<0 || mercuryCount<0)
                throw new Exception("Fabryka sprawdzila, zasoby < 0 !");

            if (isSulfur && isLead && isMercury){
                //D
                alchemistDCountMutex.WaitOne();
                if (alchemistDCount > 0){
                    alchemistDMutex.Release();
                    alchemistDCount--;
                    sulfurCount--;
                    isSulfur=sulfurCount>0;
                    sulfurFull.Release();
                    leadCount--;
                    isLead = leadCount>0;
                    leadFull.Release();
                    mercuryCount--;
                    isMercury=mercuryCount>0;
                    mercuryFull.Release();
                }
                alchemistDCountMutex.Release();
            }
            if (isSulfur && isMercury){
                //B
                alchemistBCountMutex.WaitOne();
                if (alchemistBCount > 0){
                    alchemistBMutex.Release();
                    alchemistBCount--;
                    sulfurCount--;
                    isSulfur=sulfurCount>0;
                    sulfurFull.Release();
                    mercuryCount--;
                    isMercury=mercuryCount>0;
                    mercuryFull.Release();
                }
                alchemistBCountMutex.Release();
            }
            if (isMercury && isLead){
                //A
                alchemistACountMutex.WaitOne();
                if (alchemistACount > 0){
                    alchemistAMutex.Release();                
                    alchemistACount--;
                    leadCount--;
                    isLead = leadCount>0;
                    leadFull.Release();
                    mercuryCount--;
                    isMercury=mercuryCount>0;
                    mercuryFull.Release();
                }

                alchemistACountMutex.Release();
            }
            if (isLead && isSulfur){
                //C
                alchemistCCountMutex.WaitOne();
                if (alchemistCCount > 0){
                    alchemistCMutex.Release();
                    alchemistCCount--;
                    leadCount--;
                    isLead = leadCount>0;
                    leadFull.Release();
                    sulfurCount--;
                    isSulfur=sulfurCount>0;
                    sulfurFull.Release();
                }
                alchemistCCountMutex.Release();
            }
        }

        private bool checkResourcesAlchemist (char type){
            bool isSulfur = sulfurCount>0;
            bool isLead = leadCount>0;
            bool isMercury = mercuryCount>0;
            if (leadCount<0 || sulfurCount<0 || mercuryCount<0)
                throw new Exception("Alchemik sprawdzil, zasoby < 0 !");

            if (type=='A'){
                if (isMercury && isLead){
                    mercuryCount--;
                    mercuryFull.Release();
                    leadCount--;
                    leadFull.Release();
                    return true;
                }
            }
            else if (type=='B'){
                if (isSulfur && isMercury){
                    sulfurCount--;
                    sulfurFull.Release();
                    mercuryCount--;
                    mercuryFull.Release();
                    return true;
                }
            }
            else if (type=='C'){
                if (isLead && isSulfur){
                    leadCount--;
                    leadFull.Release();
                    sulfurCount--;
                    sulfurFull.Release();
                    return true;
                }
            }
            else if (type=='D'){
                if(isLead && isSulfur && isMercury){
                    leadCount--;
                    leadFull.Release();
                    sulfurCount--;
                    sulfurFull.Release();
                    mercuryCount--;
                    mercuryFull.Release();
                    return true;
                }
            }
            return false;
        }
        
        public void WarlockWork (){
            //odpalany co pewien czas
            //poczekaj na mutexie od cursecount
            //zwieksz wartosc
            //jezeli == 1, wait mutex
            while(true){
                int which = r.Next(1,3);
                System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik wybiera fabryke: "+resolveResourceName(which));
                if (which == mercury){
                    System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik czeka na dostep do klatw od "+resolveResourceName(which));
                    mercuryCurseCountMutex.WaitOne();
                    mercuryCurseCount++;
                    if (mercuryCurseCount==1){
                        mercuryNotCursedMutex.WaitOne();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik przeklina fabryke "+resolveResourceName(which)+", klatw: "+mercuryCurseCount);
                    }
                    mercuryCurseCountMutex.Release();
                }
                else if (which == sulfur){
                    System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik czeka na dostep do klatw od "+resolveResourceName(which));
                    sulfurCurseCountMutex.WaitOne();
                    sulfurCurseCount++;
                    if (sulfurCurseCount==1){
                        sulfurNotCursedMutex.WaitOne();
                    System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik przeklina fabryke "+resolveResourceName(which)+", klatw: "+sulfurCurseCount);
                    }
                    sulfurCurseCountMutex.Release();
                }
                else{
                    System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik czeka na dostep do klatw od "+resolveResourceName(which));
                    leadCurseCountMutex.WaitOne();
                    leadCurseCount++;
                    if (leadCurseCount==1){
                        leadNotCursedMutex.WaitOne();
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik przeklina fabryke "+resolveResourceName(which)+", klatw: "+leadCurseCount);
                    }
                    leadCurseCountMutex.Release();
                }
                int s = r.Next(1,this.warlockMaxSleep);
                System.Console.WriteLine(System.DateTime.Now+" "+"Czarownik najebal sie, wiec spi "+s+" sekund.");
                Thread.Sleep(s*1000);
            }
        }

        public void WizardWork (){
            //odpalany co pewien czas
            //poczekaj na mutexie od cursecount
            //zmniejsz wartosc
            //jezeli == 0, signal mutex
            System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej startuje.");
            while(true){
                for (int i = 1; i < 4;i++){
                    if (i==lead){
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej czeka na dostep do klatw od "+resolveResourceName(i));
                        leadCurseCountMutex.WaitOne();
                        int oldCount = leadCurseCount;
                        leadCurseCount = Math.Max(0,leadCurseCount-1);
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej zdejmuje klatwe z fabryki "+resolveResourceName(i)+", wczesniej bylo klatw: "+oldCount);
                        if(oldCount == 1){
                            System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej calkowicie odklina fabryke "+resolveResourceName(i));
                            leadNotCursedMutex.Release();
                        }
                        leadCurseCountMutex.Release();

                    }
                    else if (i==mercury){
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej czeka na dostep do klatw od "+resolveResourceName(i));
                        mercuryCurseCountMutex.WaitOne();
                        int oldCount = mercuryCurseCount;
                        mercuryCurseCount = Math.Max(0,mercuryCurseCount-1);
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej zdejmuje klatwe z fabryki "+resolveResourceName(i)+", wczesniej bylo klatw: "+oldCount);
                        if(oldCount == 1){
                            System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej odklina fabryke "+resolveResourceName(i));
                            mercuryNotCursedMutex.Release();
                        }
                        mercuryCurseCountMutex.Release();

                    }
                    else{
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej czeka na dostep do klatw od "+resolveResourceName(i));
                        sulfurCurseCountMutex.WaitOne();
                        int oldCount = sulfurCurseCount;
                        sulfurCurseCount = Math.Max(0,sulfurCurseCount-1);
                        System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej zdejmuje klatwe z fabryki "+resolveResourceName(i)+", wczesniej bylo klatw: "+oldCount);
                        if(oldCount == 1){
                            System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej odklina fabryke "+resolveResourceName(i));
                            sulfurNotCursedMutex.Release();
                        }
                        sulfurCurseCountMutex.Release();
                    }
                }
                int s = r.Next(1,this.wizardMaxSleep);
                System.Console.WriteLine(System.DateTime.Now+" "+"Czarodziej najebal sie, wiec spi "+s+" sekund.");
                Thread.Sleep(s*1000);
            }
        }

        public void AlchemistAWork(){
            resourcesMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik A przyszedl, sprawdza czy ma dostepne zasoby.");
            if (checkResourcesAlchemist('A')){
                resourcesMutex.Release();
                System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik A odchodzi - mial wszystkie zasoby");
                return;
            }
            resourcesMutex.Release();
            alchemistACountMutex.WaitOne();
            alchemistACount++;
            alchemistACountMutex.Release();
            alchemistAMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka zwolnila alchemika A");
        }
        public void AlchemistBWork(){
            resourcesMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik B przyszedl, sprawdza czy ma dostepne zasoby.");
            if (checkResourcesAlchemist('B')){
                resourcesMutex.Release();
                System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik B odchodzi - mial wszystkie zasoby");
                return;
            }
            resourcesMutex.Release();
            alchemistBCountMutex.WaitOne();
            alchemistBCount++;
            alchemistBCountMutex.Release();
            alchemistBMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka zwolnila alchemika B");
        }
        public void AlchemistCWork(){
            resourcesMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik C przyszedl, sprawdza czy ma dostepne zasoby.");
            if (checkResourcesAlchemist('C')){
                resourcesMutex.Release();
                System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik C odchodzi - mial wszystkie zasoby");
                return;
            }
            resourcesMutex.Release();
            alchemistCCountMutex.WaitOne();
            alchemistCCount++;
            alchemistCCountMutex.Release();
            alchemistCMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka zwolnila alchemika C");
        }
        public void AlchemistDWork(){
            resourcesMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik D przyszedl, sprawdza czy ma dostepne zasoby.");
            if(checkResourcesAlchemist('D')){
                resourcesMutex.Release();
                System.Console.WriteLine(System.DateTime.Now+" "+"Alchemik D odchodzi - mial wszystkie zasoby");
                return;
            }
            resourcesMutex.Release();
            alchemistDCountMutex.WaitOne();
            alchemistDCount++;
            alchemistDCountMutex.Release();
            alchemistDMutex.WaitOne();
            System.Console.WriteLine(System.DateTime.Now+" "+"Fabryka zwolnila alchemika D");
        }

        private String resolveResourceName (int resource){
            if (resource == sulfur)
                return "\"siarka\"";
            else if (resource == lead)
                return "\"olow\"";

            return "\"rtec\"";
        }

        private void printAlchemics(){
            System.Console.WriteLine(System.DateTime.Now + " OCZEKUJACY ALCHEMICY:");
            System.Console.WriteLine("Alchemicy A - "+this.alchemistACount);
            System.Console.WriteLine("Alchemicy B - "+this.alchemistBCount);
            System.Console.WriteLine("Alchemicy C - "+this.alchemistCCount);
            System.Console.WriteLine("Alchemicy D - "+this.alchemistDCount);
            System.Console.WriteLine(System.DateTime.Now + " ZASOBY:");
            System.Console.WriteLine("Rtec - "+ this.mercuryCount);
            System.Console.WriteLine("Siarka - "+ this.sulfurCount);
            System.Console.WriteLine("Olow - "+ this.leadCount);
        }
    }
}
