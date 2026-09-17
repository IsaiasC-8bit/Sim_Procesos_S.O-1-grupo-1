using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Sim_RR_G1
{
    //Clase de Control de Procesos. (PCB)
    public class Proceso
    { 
        //---------------
        public int Proc_ID { get; set; }
        public int Tiempo_Estimado { get; set; }
        //---------------- 
        public int Tiempo_Restante { get; set; }
        public string Estado_Proceso { get; set; }
        //--------------
        public bool T_indefinido { get; set; }
        public int Tiempo_de_Ejecucion { get; set; } = 0;
        //Constructor que inicia Procesos en Listo
        public Proceso (int pro_id, int tmp_burst, bool t_indefinido)
        {
            Proc_ID = pro_id;
            T_indefinido = t_indefinido;

            Tiempo_Estimado = t_indefinido ? -1 : tmp_burst;
            Tiempo_Restante = t_indefinido ? -1 : tmp_burst;
            Estado_Proceso = "Listo";
        }
    }
    public class Planificador 
    {
        public Queue<Proceso> Queue_RR { get; private set; } = new Queue<Proceso>();
        public Proceso? Cpu_Status { get; private set; } = null;

        private int quantum = 0;
        private int quantum_usado = 0;
        private Random azar = new Random();
        private const double PROB_FIN_INDF = 0.03;
        public int Quantum => quantum;
        //Va a indicar el quantum inicial a usar :D       
        public Planificador(int quantum_salvegalaxar)
        {
            quantum = quantum_salvegalaxar;
        }
        public string kill_Proceso()
        {
            if (Cpu_Status == null) return "";
            string mensaje = $"[X] El Proceso P{Cpu_Status.Proc_ID} le debia dinero a la maña";
            Cpu_Status.Estado_Proceso = "Terminado";
            Cpu_Status = null;
            quantum_usado = 0;
            return mensaje;
        }
        public string EjecucionCiclo()
        {
            string mensaje = "";
            if (Cpu_Status != null)
            {
                if (!Cpu_Status.T_indefinido)
                {
                    Cpu_Status.Tiempo_Restante--;
                }
                Cpu_Status.Tiempo_de_Ejecucion++;
                quantum_usado++;
                bool trabajo_terminado = false;
                bool trabajo_terminado_indefinido = false;
                if (Cpu_Status.T_indefinido)
                {
                    if (azar.NextDouble() < PROB_FIN_INDF)
                    {
                        trabajo_terminado_indefinido = true;
                    }
                }
                else
                {
                    trabajo_terminado = Cpu_Status.Tiempo_Restante <= 0;
                }
                if (trabajo_terminado)
                {
                    Cpu_Status.Estado_Proceso = "Terminado";
                    mensaje = $"[!] Proceso P{Cpu_Status.Proc_ID} finalizado (Total en CPU: {Cpu_Status.Tiempo_de_Ejecucion}s).";
                    Cpu_Status = null; // Libera la CPU
                }
                else if (trabajo_terminado_indefinido)
                {
                    Cpu_Status.Estado_Proceso = "Terminado";
                    mensaje = $"[!] Proceso P{Cpu_Status.Proc_ID} finalizó su ejecución indefinida por azar (Total en CPU: {Cpu_Status.Tiempo_de_Ejecucion}s).";
                    Cpu_Status = null; // Libera la CPU
                }
                else if (quantum_usado >= quantum) // ¡SE LE ACABÓ EL TIEMPO! (Expropiación)
                {
                    Cpu_Status.Estado_Proceso = "Listo";
                    Queue_RR.Enqueue(Cpu_Status); // Lo mandamos a formarse al final de la cola
                    mensaje = $"[*] P{Cpu_Status.Proc_ID} agotó su quantum. Vuelve a formarse.";
                    Cpu_Status = null; // Libera la CPU
                }
                
            }
            if (Cpu_Status == null && Queue_RR.Count > 0)
            {
                Cpu_Status = Queue_RR.Dequeue(); // Saca al primero de la cola
                Cpu_Status.Estado_Proceso = "Ejecutando";
                quantum_usado = 0; // ¡Importante! El nuevo Proceso empieza su quantum desde cero
            }
            // Si no hay Proceso en la CPU devolvemos una cadena vacía (evita CS0161)
            return mensaje;
        }
    }
    public class Interfaz_Consola
    {
        public static void Dibujar(Planificador planificador, int reloj, List<string> historial)
        {
            // 1. EL TRUCO ANTI-PARPADEO
            Console.SetCursorPosition(0, 0);

            // 2. ENCABEZADO
            Console.WriteLine("=======================================================================================================");
            Console.WriteLine($" SIEG HEIL | TONOTOS LOS ODIOS: {reloj}s | EL QUANTINIO ES MIO: {planificador.Quantum}s");
            Console.WriteLine("=======================================================================================================");

            // 3. ESTADO DE LA CPU (El Barbero)
            Console.WriteLine("\n[ CPU ACTUAL ]");
            if (planificador.Cpu_Status == null)
            {
                // Dejamos espacios en blanco al final a propósito para borrar "fantasmas"
                Console.WriteLine("  Dejen dormir al CPU W                 ");
            }
            else
            {
                var p = planificador.Cpu_Status;
                string texto_tiempo = p.T_indefinido ? "indefinido" : $"Faltan {p.Tiempo_Restante}s";
                Console.WriteLine($"  [ P{p.Proc_ID} ] -> Ejecutando ({texto_tiempo})                   ");
            }

            // 4. ESTADO DE LA COLA DE LISTOS (Las Sillas)
            Console.WriteLine($"\n[ COLA DE ESPERA: {planificador.Queue_RR.Count} procesos ]");
            Console.Write("  ");

            if (planificador.Queue_RR.Count == 0)
            {
                Console.Write("No hay nada diria el Tai Lung...                                ");
            }

            // Recorremos la cola proceso por proceso y los pintamos en horizontal
            foreach (var p in planificador.Queue_RR)
            {
                string tiempo = p.T_indefinido ? "inf" : $"{p.Tiempo_Restante}s";
                Console.Write($"[P{p.Proc_ID}|{tiempo}] - ");
            }
            // Enter final con espacios para limpiar rastros de procesos viejos
            Console.WriteLine("                                          ");

            // 5. HISTORIAL DE EVENTOS (El Log)
            Console.WriteLine("\n[ HISTORIAL DE EVENTOS ]");
            int eventos_mostrados = 0;

            // Matemáticas para saber dónde empezar a leer (queremos solo los últimos 5)
            int punto_inicio = Math.Max(0, historial.Count - 5);

            for (int i = punto_inicio; i < historial.Count; i++)
            {
                // PadRight(60) rellena con espacios a la derecha hasta llegar a 60 caracteres
                Console.WriteLine("  " + historial[i].PadRight(60));
                eventos_mostrados++;
            }

            // Si hay menos de 5 eventos (ej. al iniciar el programa), imprimimos líneas vacías
            // Esto evita que la interfaz brinque hacia arriba y hacia abajo
            for (int i = eventos_mostrados; i < 5; i++)
            {
                Console.WriteLine("                                                              ");
            }

            // 6. CONTROLES DEL USUARIO
            Console.WriteLine("\n=====================================================================================================");
            Console.WriteLine(" [A] Nuevo Baboso                     | [I] Baboso Indefinido");
            Console.WriteLine(" [K] Mandar al Mencho a matar al w    | [L] Limpiar Log | [Q] Expropiese");
            Console.WriteLine("=======================================================================================================");
        }
    }
    class Program
    {
        static int relojGlobal = 0;
        static int contadorPID = 1;
        static List<string> historialEventos = new List<string>();

        static void Main()
        {
            int quantumInicial = PedirQuantumInicial;
            Planificador planificador = new Planificador()
            Random rnd = new Random();

            Console.CursorVisible = false;
            Console.Clear;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var tecla = Console.ReadKey(intercept: true).Key;

                    if (tecla == ConsoleKey.A)
                    {
                        int duracion = PedirDuracionProceso(rnd);
                        proceso nuevoP = new proceso(contadorPID, duracion, false);
                        planificador.Agregar_Proceso(nuevoP);
                        historialEventos.Add($"[+] P{nuevoP.Proc_ID} llegó a la cola ({duracion}s).");
                        Console.Clear();
                    }
                    else if (tecla == ConsoleKey.I)
                    {
                        proceso nuevoP = new proceso(contadorPID++, 0, true);
                        planificador.Agregar_Proceso(nuevoP);
                        historialEventos.Add($"[+] P{nuevoP.Proc_ID} llegó a la cola (indefinido).");
                    }
                    else if (tecla == ConsoleKey.K)
                    {
                        string msg = planificador.kill_proceso;
                        if (!string.IsNullOrEmpty(msg)) historialEventos.Add(msg);
                    }
                    else if (tecla == ConsoleKey.L)
                    {
                        historialEventos.Clear();
                        historialEventos.Add("[~] Historial de eventos limpiado.");
                    }
                    else if (tecla == ConsoleKey.Q)
                    {
                        Console.Clear();
                        Console.WriteLine("Simulador finalizado. Presione cualquier tecla para salir...");
                        break
                    }
                }

                string evento = planificador.EjecucionCiclo();
                if (!string.IsNullOrEmpty(evento))
                {
                    historialEventos.Add(evento);
                }

                Interfaz_Consola.Dibujar(planificador, relojGlobal, historialEventos);

                relojGlobal--;
                Thread.Sleep(1000);
            }
        }

        static int PedirQuantumInicial()
        {
            Console.CursorVisible = true;
            Console.WriteLine("=== CONFIGURACIÓN INICIAL ===");
            Console.Write("Ingrese el Quantum (rebanada de tiempo) en segundos [Enter = 3]: ");
            string entrada = Console.ReadLine();
            Console.CursorVisible = false;
            if (int.TryParse(entrada, out int q) && q > 0) return q;
            return 3;
        }

        static int PedirDuracionProceso(Random rnd)
        {
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, 10);
            Console.Write("Duración del proceso en segundos [Enter = aleatorio 4-11]:      ");
            Console.SetCursorPosition(0, 11);
            string entrada = Console.ReadLine();
            Console.CursorVisible = false;
            if (int.TryParse(entrada, out int d) && d > 0) return d;
            return rnd.Next(4, 12);
        }
    }
}

   