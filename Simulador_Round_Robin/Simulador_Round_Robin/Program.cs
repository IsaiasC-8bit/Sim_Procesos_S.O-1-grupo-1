using Sim_RR_G1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Sim_RR_G1
{
    //Clase de Control de Procesos. (PCB)
    public class Proceso
    { 
    // Constantes con los tres estados posibles del proceso, evitan errores de texto
    public const string ESTADO_LISTO = "Listo";
    public const string ESTADO_EJECUTANDO = "Ejecutando";
    public const string ESTADO_TERMINADO = "Terminado";

    // ID unico que identifica al proceso
    public int Proc_ID { get; set; }
    // Nombre visible del proceso, autogenerado o personalizado
    public string Nombre { get; set; }
    // Duracion total que necesita el proceso (-1 si es indefinido)
    public int Tiempo_Estimado { get; set; }
    // Tiempo que le falta por ejecutar, lo descuenta el Planificador
    public int Tiempo_Restante { get; set; }
    // Estado actual del proceso dentro de la simulacion
    public string Estado_Proceso { get; set; }
    // Indica si el proceso no tiene una duracion fija
    public bool T_indefinido { get; set; }
    // Tiempo real acumulado que el proceso ha usado la CPU
    public int Tiempo_de_Ejecucion { get; set; } = 0;

    // Constructor: crea el proceso y lo deja listo para entrar a la cola
    public Proceso(int pro_id, int tmp_burst, bool t_indefinido, string? nombre = null)
    {
    // Si el ID no es valido, se detiene la creacion con un error claro
    if (pro_id <= 0)
        throw new ArgumentException("El ID del proceso debe ser mayor a 0.", nameof(pro_id));

    // Si el proceso es finito pero su duracion es invalida, tambien se detiene
    if (!t_indefinido && tmp_burst <= 0)
        throw new ArgumentException("La duracion del proceso debe ser mayor a 0.", nameof(tmp_burst));

    // Asigna el ID recibido
    Proc_ID = pro_id;
    // Usa el nombre dado, o genera uno automatico si no se especifico
    Nombre = string.IsNullOrWhiteSpace(nombre) ? $"Proceso-{pro_id}" : nombre;
    // Guarda si el proceso es indefinido
    T_indefinido = t_indefinido;

    // Si es indefinido, el tiempo estimado no aplica (-1); si no, usa el burst dado
    Tiempo_Estimado = t_indefinido ? -1 : tmp_burst;
    // Igual que arriba, pero para el tiempo restante inicial
    Tiempo_Restante = t_indefinido ? -1 : tmp_burst;
    // Todo proceso nuevo arranca en estado "Listo"
    Estado_Proceso = ESTADO_LISTO;
    }

    // Devuelve una version legible del proceso, util para mostrar en consola o depurar
    public override string ToString()
    {
    // Arma el texto del tiempo segun si es indefinido o no
    string tiempo = T_indefinido ? "indefinido" : $"{Tiempo_Restante}s restantes";
    // Devuelve todo junto en un solo string
    return $"[{Nombre} | ID={Proc_ID} | {Estado_Proceso} | {tiempo}]";
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

        // Constructor con validación para evitar Quantum inválido (0 o negativo)
        public Planificador(int quantum_salvegalaxar)
        {
            quantum = quantum_salvegalaxar > 0 ? quantum_salvegalaxar : 3;
        }

        // Método para agregar un nuevo proceso a la cola de espera
        public void Agregar_Proceso(Proceso p)
        {
            p.Estado_Proceso = "Listo";
            Queue_RR.Enqueue(p);
        }

        // Cancelar o forzar la salida del proceso actual de la CPU
        public string kill_Proceso()
        {
            if (Cpu_Status == null) return string.Empty;

            string mensaje = $"[X] El Proceso P{Cpu_Status.Proc_ID} fue terminado manualmente.";
            Cpu_Status.Estado_Proceso = "Terminado";
            Cpu_Status = null;
            quantum_usado = 0;
            return mensaje;
        }

        // Lógica de ejecución por cada ciclo de reloj
        public string EjecucionCiclo()
        {
            string mensaje = string.Empty;

            if (Cpu_Status != null)
            {
                if (!Cpu_Status.T_indefinido)
                {
                    Cpu_Status.Tiempo_Restante--;
                }

                Cpu_Status.Tiempo_de_Ejecucion++;
                quantum_usado++;

                // Evalúa si el trabajo terminó (finito o indefinido por probabilidad)
                bool trabajoTerminado = Cpu_Status.T_indefinido 
                    ? azar.NextDouble() < PROB_FIN_INDF 
                    : Cpu_Status.Tiempo_Restante <= 0;

                if (trabajoTerminado)
                {
                    Cpu_Status.Estado_Proceso = "Terminado";
                    mensaje = Cpu_Status.T_indefinido 
                        ? $"[!] Proceso P{Cpu_Status.Proc_ID} finalizó su ejecución indefinida (Total: {Cpu_Status.Tiempo_de_Ejecucion}s)."
                        : $"[!] Proceso P{Cpu_Status.Proc_ID} finalizado (Total en CPU: {Cpu_Status.Tiempo_de_Ejecucion}s).";
                    
                    Cpu_Status = null; // Liberar la CPU
                }
                else if (quantum_usado >= quantum) // Expropiación por fin de Quantum
                {
                    // Guardamos la referencia no nula para evitar errores de compilación
                    Proceso procesoActual = Cpu_Status;

                    procesoActual.Estado_Proceso = "Listo";
                    Queue_RR.Enqueue(procesoActual); // Vuelve al final de la cola
                    mensaje = $"[*] P{procesoActual.Proc_ID} agotó su quantum. Vuelve a formarse.";
                    Cpu_Status = null; // Liberar la CPU
                }
            }

            // Si la CPU quedó libre, se asigna el siguiente de la cola
            if (Cpu_Status == null && Queue_RR.Count > 0)
            {
                Cpu_Status = Queue_RR.Dequeue();
                Cpu_Status.Estado_Proceso = "Ejecutando";
                quantum_usado = 0; // El nuevo proceso inicia su quantum de cero
            }

            return mensaje;
        }
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
namespace Simulador_Round_Robin //Angel Roquel
{
    class Program
    {
        static int relojGlobal = 0;
        static int contadorPID = 1;
        static List<string> historialEventos = new List<string>();

        static void Main()
        {
            int quantumInicial = PedirQuantumInicial();
            Planificador planificador = new Planificador(quantumInicial);
            Random rnd = new Random();

            Console.CursorVisible = false;
            Console.Clear();

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var tecla = Console.ReadKey(intercept: true).Key;

                    if (tecla == ConsoleKey.A)
                    {
                        int duracion = PedirDuracionProceso(rnd);
                        Proceso nuevoP = new Proceso(contadorPID, duracion, false);
                        planificador.Agregar_Proceso(nuevoP);
                        historialEventos.Add($"[+] P{nuevoP.Proc_ID} llegó a la cola ({duracion}s).");
                        Console.Clear();
                    }
                    else if (tecla == ConsoleKey.I)
                    {
                        Proceso nuevoP = new Proceso(contadorPID++, 0, true);
                        planificador.Agregar_Proceso(nuevoP);
                        historialEventos.Add($"[+] P{nuevoP.Proc_ID} llegó a la cola (indefinido).");
                    }
                    else if (tecla == ConsoleKey.K)
                    {
                        string msg = planificador.kill_Proceso();
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
                        break;
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
   