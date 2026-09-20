using System;
using System.Collections.Generic;
using System.Threading;

namespace Sim_RR_G1
{
    //Revision final por Roli Cedillo
    //Miembros del grupo: Herson Omar Giron Quixtan, Xander Jade Reyes Orellana , Angel Andres Roquel Mejia
    //Coordinador: Roli Isaias Cedillo Chay

    //Clase Creada por Roli Cedillo
    public class EventoLog
    {
        public string Mensaje { get; set; }
        public ConsoleColor ColorTema { get; set; }

        public EventoLog(string mensaje, ConsoleColor colorTema)
        {
            Mensaje = mensaje;
            ColorTema = colorTema;
        }
    }
    //Clase Creada por Herson Giron
    public class Proceso
    {
        // Constantes de estado (evitan errores tipográficos al cambiar estados)
        public const string ESTADO_LISTO = "Listo";
        public const string ESTADO_EJECUTANDO = "Ejecutando";
        public const string ESTADO_TERMINADO = "Terminado";

        public int Proc_ID { get; set; }
        public string Nombre { get; set; }
        public int Tiempo_Estimado { get; set; } // Burst Time original
        public int Tiempo_Restante { get; set; } // Tiempo que le falta para concluir
        public string Estado_Proceso { get; set; }
        public bool T_indefinido { get; set; } // Flag para procesos tipo daemon/servicios de fondo
        public int Tiempo_de_Ejecucion { get; set; } = 0; // Tiempo real acumulado en CPU

        public ConsoleColor ColorTema { get; set; }

        // Paleta de colores restringida a tonos brillantes para alta legibilidad en consola negra
        private static readonly Random colorRnd = new Random();
        private static readonly ConsoleColor[] coloresPermitidos = {
            ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow,
            ConsoleColor.Magenta, ConsoleColor.Red, ConsoleColor.DarkYellow, ConsoleColor.Blue, ConsoleColor.DarkGreen
        };

        public Proceso(int pro_id, int tmp_burst, bool t_indefinido, string? nombre = null)
        {
            // Validaciones de integridad del proceso
            if (pro_id <= 0)
                throw new ArgumentException("El ID del proceso debe ser mayor a 0.", nameof(pro_id));

            if (!t_indefinido && tmp_burst <= 0)
                throw new ArgumentException("La duración del proceso debe ser mayor a 0.", nameof(tmp_burst));

            Proc_ID = pro_id;
            Nombre = string.IsNullOrWhiteSpace(nombre) ? $"Proceso-{pro_id}" : nombre;
            T_indefinido = t_indefinido;

            // Si es indefinido, el tiempo estimado y restante se fijan en -1 (infinito)
            Tiempo_Estimado = t_indefinido ? -1 : tmp_burst;
            Tiempo_Restante = t_indefinido ? -1 : tmp_burst;

            // Todo proceso entra al sistema en estado de espera (Ready Queue)
            Estado_Proceso = ESTADO_LISTO;

            // Asignación de color aleatorio para la interfaz visual
            ColorTema = coloresPermitidos[colorRnd.Next(coloresPermitidos.Length)];
        }

        public override string ToString()
        {
            string tiempo = T_indefinido ? "indefinido" : $"{Tiempo_Restante}s restantes";
            return $"[{Nombre} | ID={Proc_ID} | {Estado_Proceso} | {tiempo}]";
        }
    }

    //Clase Creada por Xander Reyes
    public class Planificador
    {
        public Queue<Proceso> Queue_RR { get; private set; } = new Queue<Proceso>();
        public Proceso? Cpu_Status { get; private set; } = null; // Representa el núcleo del procesador

        private int quantum = 0;
        private int quantum_usado = 0; // Cronómetro interno del contexto actual
        private Random azar = new Random();

        // Probabilidad de que un proceso indefinido haga "exit()" en un ciclo dado
        private const double PROB_FIN_INDF = 0.03;

        public int Quantum => quantum;

        public Planificador(int quantum_inicial)
        {
            // Se asegura de que el Quantum nunca sea 0 para evitar bucles infinitos
            quantum = quantum_inicial > 0 ? quantum_inicial : 3;
        }

        /// <summary>
        /// Forma un proceso en la cola de listos (Ready Queue).
        /// </summary>
        public void Agregar_Proceso(Proceso p)
        {
            p.Estado_Proceso = "Listo";
            Queue_RR.Enqueue(p);
        }

        /// <summary>
        /// Simula una interrupción de hardware o señal de sistema (SIGKILL) para terminar el proceso actual.
        /// </summary>
        public EventoLog? kill_Proceso()
        {
            if (Cpu_Status == null) return null;

            string mensaje = $"[X] El Proceso P{Cpu_Status.Proc_ID} fue terminado manualmente.";
            ConsoleColor colorProceso = Cpu_Status.ColorTema;

            Cpu_Status.Estado_Proceso = "Terminado";
            Cpu_Status = null; // Expulsa el proceso de la CPU
            quantum_usado = 0; // Reinicia el temporizador del planificador

            return new EventoLog(mensaje, colorProceso);
        }

        public EventoLog? EjecucionCiclo()
        {
            EventoLog? eventoGenerado = null;

            // 1. FASE DE EJECUCIÓN (Si la CPU está ocupada)
            if (Cpu_Status != null)
            {
                if (!Cpu_Status.T_indefinido)
                {
                    Cpu_Status.Tiempo_Restante--; // Desgaste del Burst Time
                }

                Cpu_Status.Tiempo_de_Ejecucion++;
                quantum_usado++;

                // 2. FASE DE EVALUACIÓN (¿El proceso debe salir de la CPU?)
                bool trabajoTerminado = Cpu_Status.T_indefinido
                    ? azar.NextDouble() < PROB_FIN_INDF
                    : Cpu_Status.Tiempo_Restante <= 0;

                if (trabajoTerminado)
                {
                    Cpu_Status.Estado_Proceso = "Terminado";
                    string msj = Cpu_Status.T_indefinido
                        ? $"[!] Proceso P{Cpu_Status.Proc_ID} finalizó su ejecución indefinida (Total: {Cpu_Status.Tiempo_de_Ejecucion}s)."
                        : $"[!] Proceso P{Cpu_Status.Proc_ID} finalizado (Total en CPU: {Cpu_Status.Tiempo_de_Ejecucion}s).";

                    eventoGenerado = new EventoLog(msj, Cpu_Status.ColorTema);
                    Cpu_Status = null; // Liberación voluntaria de CPU
                }
                else if (quantum_usado >= quantum)
                {
                    // EXPROPIACIÓN: El SO interrumpe al proceso por tiempo agotado
                    Proceso procesoActual = Cpu_Status;
                    procesoActual.Estado_Proceso = "Listo";
                    Queue_RR.Enqueue(procesoActual); // Reingresa al final de la cola

                    string msj = $"[*] P{procesoActual.Proc_ID} agotó su quantum. Vuelve a formarse.";
                    eventoGenerado = new EventoLog(msj, procesoActual.ColorTema);
                    Cpu_Status = null; // Liberación forzada de CPU
                }
            }

            // 3. FASE DE DESPACHO (Dispatcher)
            // Si la CPU está libre y hay procesos esperando, inyecta el siguiente
            if (Cpu_Status == null && Queue_RR.Count > 0)
            {
                Cpu_Status = Queue_RR.Dequeue();
                Cpu_Status.Estado_Proceso = "Ejecutando";
                quantum_usado = 0; // El reloj del Quantum se reinicia para el nuevo proceso
            }

            return eventoGenerado;
        }
    }

   //Clase creada por Roli Cedillo
    public class Interfaz_Consola
    {
        // CORRECCIÓN: Se cambió List<string> a List<EventoLog> para coincidir con la lista enviada por el Main.
        public static void Dibujar(Planificador planificador, int reloj, List<EventoLog> historial, bool mostrarAyuda = false)
        {
            Console.SetCursorPosition(0, 0);

            // ENCABEZADO
            Console.WriteLine("=====================================================================================================");
            Console.WriteLine($" SIMULADOR ROUND ROBIN | RELOJ DEL SISTEMA: {reloj}s | QUANTUM: {planificador.Quantum}s".PadRight(100));
            Console.WriteLine("=====================================================================================================");

            // RENDERIZADO CONDICIONAL: Manual de Ayuda vs Pantalla de Simulación
            if (mostrarAyuda)
            {
                Console.WriteLine("\n[ MANUAL DE USUARIO - AYUDA ]".PadRight(100));
                Console.WriteLine("  El algoritmo Round Robin asigna un tiempo máximo (Quantum) a cada proceso en la CPU.".PadRight(100));
                Console.WriteLine("  Si el proceso no termina en ese tiempo, es enviado al final de la cola (Expropiación).".PadRight(100));
                Console.WriteLine(" ".PadRight(100));
                Console.WriteLine("  TECLAS DE CONTROL:".PadRight(100));
                Console.WriteLine("  [A] - Crea un proceso con una duración fija. Terminará al llegar a 0.".PadRight(100));
                Console.WriteLine("  [I] - Crea un proceso infinito. Tiene probabilidad de terminar solo en cada ciclo.".PadRight(100));
                Console.WriteLine("  [K] - Termina a la fuerza ('mata') el proceso que está actualmente en la CPU.".PadRight(100));
                Console.WriteLine("  [L] - Limpia el historial de eventos de la pantalla.".PadRight(100));
                Console.WriteLine("  [H] - Muestra u oculta este manual de ayuda.".PadRight(100));
                Console.WriteLine("  [Q] - Finaliza el simulador por completo.".PadRight(100));

                // Relleno estructural para mantener las proporciones de la consola estables
                for (int i = 0; i < 7; i++) Console.WriteLine("".PadRight(100));
            }
            else
            {
                // VISTA PRINCIPAL DEL SIMULADOR
                Console.WriteLine("\n[ CPU ACTUAL ]".PadRight(100));

                if (planificador.Cpu_Status == null)
                {
                    Console.WriteLine("  (Inactivo) CPU Libre. Esperando procesos...".PadRight(100));
                }
                else
                {
                    var p = planificador.Cpu_Status;
                    string texto_tiempo = p.T_indefinido ? "indefinido" : $"Faltan {p.Tiempo_Restante}s";

                    Console.Write("  [ ");
                    Console.ForegroundColor = p.ColorTema;
                    Console.Write($"P{p.Proc_ID}");
                    Console.ResetColor();
                    Console.WriteLine($" ] -> Ejecutando ({texto_tiempo})".PadRight(80));
                }

                Console.WriteLine($"\n[ COLA DE ESPERA: {planificador.Queue_RR.Count} procesos ]".PadRight(100));
                Console.Write("  ");

                if (planificador.Queue_RR.Count == 0)
                {
                    Console.Write("[ Cola vacía ]".PadRight(98));
                }
                else
                {
                    int caracteresImpresos = 2; // Acumulador para calcular espacios sobrantes (padding dinámico)

                    foreach (var p in planificador.Queue_RR)
                    {
                        string tiempo = p.T_indefinido ? "inf" : $"{p.Tiempo_Restante}s";

                        Console.Write("[");
                        Console.ForegroundColor = p.ColorTema;
                        Console.Write($"P{p.Proc_ID}");
                        Console.ResetColor();
                        Console.Write($"|{tiempo}] - ");

                        caracteresImpresos += 6 + p.Proc_ID.ToString().Length + tiempo.Length;
                    }
                    // Relleno final para borrar "fantasmas" de caracteres en líneas que se encogen
                    Console.Write(new string(' ', Math.Max(0, 100 - caracteresImpresos)));
                }
                Console.WriteLine();

                // HISTORIAL DE EVENTOS
                Console.WriteLine("\n[ HISTORIAL DE EVENTOS ]".PadRight(100));
                int eventos_mostrados = 0;
                int punto_inicio = Math.Max(0, historial.Count - 5); // Limita la visualización a los últimos 5

                for (int i = punto_inicio; i < historial.Count; i++)
                {
                    Console.Write("  ");
                    Console.ForegroundColor = historial[i].ColorTema; // Aplica el color del proceso al log
                    Console.WriteLine(historial[i].Mensaje.PadRight(98));
                    Console.ResetColor();

                    eventos_mostrados++;
                }

                // Imprime líneas en blanco si hay menos de 5 eventos para no deformar la interfaz
                for (int i = eventos_mostrados; i < 5; i++)
                {
                    Console.WriteLine("".PadRight(100));
                }
            }

            // PIE DE PÁGINA (Siempre visible)
            Console.WriteLine("\n=====================================================================================================");
            Console.WriteLine(" [A] Nuevo Proceso | [I] Proceso Indefinido | [H] Ayuda/Manual (On/Off)".PadRight(101));
            Console.WriteLine(" [K] Terminar CPU  | [L] Limpiar Historial  | [Q] Salir del Simulador".PadRight(101));
            Console.WriteLine("=====================================================================================================");
        }
    }

   //Clase creada por Angel Roquel
    class Program
    {
        static int relojGlobal = 0;
        static int contadorPID = 1; // Asignador secuencial de IDs para los procesos
        static List<EventoLog> historialEventos = new List<EventoLog>();
        static bool HelpButton = false;

        static void Main()
        {
            // Configuración pre-arranque
            int quantumInicial = PedirQuantumInicial();
            Planificador planificador = new Planificador(quantumInicial);
            Random rnd = new Random();

            Console.CursorVisible = false;
            Console.Clear();

            // Bucle principal del sistema (Ciclo infinito del procesador)
            while (true)
            {
                // LECTURA ASÍNCRONA: No bloquea la ejecución mientras se espera una tecla
                if (Console.KeyAvailable)
                {
                    var tecla = Console.ReadKey(intercept: true).Key;

                    if (tecla == ConsoleKey.A)
                    {
                        int duracion = PedirDuracionProceso(rnd);
                        Proceso nuevoP = new Proceso(contadorPID++, duracion, false);
                        planificador.Agregar_Proceso(nuevoP);

                        historialEventos.Add(new EventoLog($"[+] P{nuevoP.Proc_ID} llegó a la cola ({duracion}s).", nuevoP.ColorTema));
                        Console.Clear();
                    }
                    else if (tecla == ConsoleKey.I)
                    {
                        Proceso nuevoP = new Proceso(contadorPID++, 0, true);
                        planificador.Agregar_Proceso(nuevoP);
                        historialEventos.Add(new EventoLog($"[+] P{nuevoP.Proc_ID} llegó a la cola (indefinido).", nuevoP.ColorTema));
                    }
                    else if (tecla == ConsoleKey.K)
                    {
                        EventoLog? eventoMatar = planificador.kill_Proceso();
                        if (eventoMatar != null) historialEventos.Add(eventoMatar);
                    }
                    else if (tecla == ConsoleKey.L)
                    {
                        historialEventos.Clear();
                        // Eventos del sistema genéricos se imprimen en gris estándar
                        historialEventos.Add(new EventoLog("[~] Historial de eventos limpiado.", ConsoleColor.Gray));
                    }
                    else if (tecla == ConsoleKey.H)
                    {
                        HelpButton = !HelpButton; // Alterna el estado de visualización
                        Console.Clear(); // Obliga un repintado completo para evitar bugs gráficos
                    }
                    else if (tecla == ConsoleKey.Q)
                    {
                        Console.Clear();
                        Console.WriteLine("Simulador finalizado. Presione cualquier tecla para salir...");
                        break;
                    }
                }

                // AVANCE LÓGICO DEL SISTEMA
                // El simulador de CPU y el reloj solo avanzan si la ayuda no está desplegada
                if (!HelpButton)
                {
                    EventoLog? eventoCiclo = planificador.EjecucionCiclo();
                    if (eventoCiclo != null)
                    {
                        historialEventos.Add(eventoCiclo);
                    }
                    relojGlobal++;
                }

                // PINTADO DE INTERFAZ
                Interfaz_Consola.Dibujar(planificador, relojGlobal, historialEventos, HelpButton);

                // DELAY DEL RELOJ: Simula el transcurso de 1 segundo real
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
            return 3; // Valor de fallback
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
            return rnd.Next(4, 12); // Valor de fallback
        }
    }
}