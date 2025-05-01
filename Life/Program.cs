using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Text.Json;
using ScottPlot;
using System.Globalization;


namespace cli_life
{
    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        public bool IsAliveNext;
        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Where(x => x.IsAlive).Count();
            if (IsAlive)
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            else
                IsAliveNext = liveNeighbors == 3;
        }
        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }
    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;
        public int Generation { get; private set; }
        public int AliveCells { get; private set; }

        public int Columns { get { return Cells.GetLength(0); } }
        public int Rows { get { return Cells.GetLength(1); } }
        public int Width { get { return Columns * CellSize; } }
        public int Height { get { return Rows * CellSize; } }

        public Board(int width, int height, int cellSize, double liveDensity = .1)
        {
            CellSize = cellSize;
            Generation = 0;
            AliveCells = 0;

            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();

            ConnectNeighbors();
            Randomize(liveDensity);
        }

        readonly Random rand = new Random();
        public void Randomize(double liveDensity)
        {
            AliveCells = 0;
            foreach (var cell in Cells)
            {
                cell.IsAlive = rand.NextDouble() < liveDensity;
                if (cell.IsAlive) AliveCells++;
            }
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            AliveCells = 0;
            foreach (var cell in Cells)
            {
                cell.Advance();
                if (cell.IsAlive) AliveCells++;
            }
        }
        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = x > 0 ? x - 1 : Columns - 1;
                    int xR = x < Columns - 1 ? x + 1 : 0;

                    int yT = y > 0 ? y - 1 : Rows - 1;
                    int yB = y < Rows - 1 ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }
        public void SaveToFile(string filename)
        {
            using (StreamWriter writer = new StreamWriter(filename))
            {
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        writer.Write(Cells[x, y].IsAlive ? '1' : '0');
                    }
                    writer.WriteLine();
                }
            }
        }

        public void LoadFromFile(string filename)
        {
            string[] lines = File.ReadAllLines(filename);
            for (int y = 0; y < Rows && y < lines.Length; y++)
            {
                for (int x = 0; x < Columns && x < lines[y].Length; x++)
                {
                    Cells[x, y].IsAlive = lines[y][x] == '1';
                }
            }
            UpdateAliveCount();
        }
        private void UpdateAliveCount()
        {
            AliveCells = 0;
            foreach (var cell in Cells)
            {
                if (cell.IsAlive) AliveCells++;
            }
        }
        public List<HashSet<(int, int)>> FindClusters()
        {
            var clusters = new List<HashSet<(int, int)>>();
            var visited = new bool[Columns, Rows];

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (Cells[x, y].IsAlive && !visited[x, y])
                    {
                        var cluster = new HashSet<(int, int)>();
                        ExploreCluster(x, y, visited, cluster);
                        clusters.Add(cluster);
                    }
                }
            }

            return clusters;
        }

        private void ExploreCluster(int x, int y, bool[,] visited, HashSet<(int, int)> cluster)
        {
            if (x < 0 || x >= Columns || y < 0 || y >= Rows ||
                !Cells[x, y].IsAlive || visited[x, y])
            {
                return;
            }

            visited[x, y] = true;
            cluster.Add((x, y));

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    int nx = (x + i + Columns) % Columns;
                    int ny = (y + j + Rows) % Rows;
                    ExploreCluster(nx, ny, visited, cluster);
                }
            }
        }
    }
    public class GameSettings
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 20;
        public int CellSize { get; set; } = 1;
        public int SimulationDelay { get; set; } = 100;
        public int StableGenerationsThreshold { get; set; } = 10;
        public int ResearchIterations { get; set; } = 10;
        public double[] ResearchDensities { get; set; } = new double[] { 0.1, 0.2, 0.3, 0.4, 0.5 };
    }
    public class PatternClassifier
    {
        public string ClassifyPattern(HashSet<(int x, int y)> cluster, Board board)
        {
            int size = cluster.Count;
            if (size == 4 && IsBlock(cluster, board)) return "Block (Still Life)";
            if (size == 6 && IsBeehive(cluster, board)) return "Beehive (Still Life)";
            if (size == 5 && IsGlider(cluster, board)) return "Glider (Spaceship)";
            if (size == 3 && IsBlinker(cluster, board)) return "Blinker (Oscillator)";
            if (size == 7 && IsToad(cluster, board)) return "Toad (Oscillator)";
            if (size == 9 && IsLWSS(cluster, board)) return "LWSS (Spaceship)";
            if (size == 9 && IsPulsar(cluster, board)) return "Pulsar (Oscillator)";

            return "Unknown pattern";
        }


        private bool IsBeehive(HashSet<(int x, int y)> cluster, Board board)
        {
            var coords = cluster.ToList();
            int minX = coords.Min(c => c.x);
            int maxX = coords.Max(c => c.x);
            int minY = coords.Min(c => c.y);
            int maxY = coords.Max(c => c.y);

            if (maxX - minX != 3 || maxY - minY != 2) return false;

            return cluster.Contains((minX + 1, minY)) &&
                   cluster.Contains((minX + 2, minY)) &&
                   cluster.Contains((minX, minY + 1)) &&
                   cluster.Contains((minX + 3, minY + 1)) &&
                   cluster.Contains((minX + 1, minY + 2)) &&
                   cluster.Contains((minX + 2, minY + 2));
        }
        private bool IsBlock(HashSet<(int x, int y)> cluster, Board board)
        {
            var coords = cluster.ToList();
            int minX = coords.Min(c => c.x);
            int maxX = coords.Max(c => c.x);
            int minY = coords.Min(c => c.y);
            int maxY = coords.Max(c => c.y);

            return maxX - minX == 1 && maxY - minY == 1;
        }

        private bool IsGlider(HashSet<(int x, int y)> cluster, Board board)
        {
            var coords = cluster.ToList();
            int minX = coords.Min(c => c.x);
            int minY = coords.Min(c => c.y);

            return cluster.Contains((minX, minY + 2)) &&
                   cluster.Contains((minX + 1, minY)) &&
                   cluster.Contains((minX + 1, minY + 2)) &&
                   cluster.Contains((minX + 2, minY + 1)) &&
                   cluster.Contains((minX + 2, minY + 2));
        }

        private bool IsBlinker(HashSet<(int x, int y)> cluster, Board board)
        {
            var coords = cluster.ToList();

            bool allSameRow = coords.Select(c => c.y).Distinct().Count() == 1;
            bool allSameCol = coords.Select(c => c.x).Distinct().Count() == 1;

            return allSameRow || allSameCol;
        }
        private bool IsLWSS(HashSet<(int x, int y)> cluster, Board board)
        {
            if (cluster.Count != 9) return false;

            var coords = cluster.ToList();
            int minX = coords.Min(c => c.x);
            int minY = coords.Min(c => c.y);

            return cluster.Contains((minX, minY)) &&
                   cluster.Contains((minX + 3, minY)) &&
                   cluster.Contains((minX + 4, minY + 1)) &&
                   cluster.Contains((minX, minY + 2)) &&
                   cluster.Contains((minX + 4, minY + 2)) &&
                   cluster.Contains((minX + 1, minY + 3)) &&
                   cluster.Contains((minX + 2, minY + 3)) &&
                   cluster.Contains((minX + 3, minY + 3)) &&
                   cluster.Contains((minX + 4, minY + 3));
        }
        private bool IsToad(HashSet<(int x, int y)> cluster, Board board)
        {
            var coords = cluster.ToList();
            int minX = coords.Min(c => c.x);
            int minY = coords.Min(c => c.y);

            return cluster.Contains((minX + 1, minY)) &&
                   cluster.Contains((minX + 2, minY)) &&
                   cluster.Contains((minX + 3, minY)) &&
                   cluster.Contains((minX, minY + 1)) &&
                   cluster.Contains((minX + 1, minY + 1)) &&
                   cluster.Contains((minX + 2, minY + 1));
        }

        private bool IsPulsar(HashSet<(int x, int y)> cluster, Board board)
        {
            return cluster.Count == 9;
        }
    }

    class Program
    {
        static Board board;
        static GameSettings settings;
        static string settingsFile = "settings.json";
        static string patternsDir = "..\\..\\..\\Patterns";
        static private void Reset()
        {
            board = new Board(
                width: 50,
                height: 20,
                cellSize: 1,
                liveDensity: 0.5);
        }
        private static void PlotStabilizationGraph()
        {
            if (!File.Exists("data.txt"))
            {
                Console.WriteLine("Research data not found. Run research mode first.");
                return;
            }

            var lines = File.ReadAllLines("data.txt");
            if (lines.Length <= 1)
            {
                Console.WriteLine("Not enough data in research file.");
                return;
            }

            var data = lines
                .Skip(1)
                .Select(line => line.Split('\t'))
                .Where(parts => parts.Length == 4)
                .Select(parts => new
                {
                    Density = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Iteration = int.Parse(parts[1]),
                    Generations = int.Parse(parts[2]),
                    AliveCells = int.Parse(parts[3])
                })
                .GroupBy(x => x.Density)
                .OrderBy(g => g.Key)
                .ToList();

            if (!data.Any())
            {
                Console.WriteLine("No valid data to plot.");
                return;
            }

            var plot = new Plot();
            plot.Title("Stabilization Time vs Density");
            plot.XLabel("Density");
            plot.YLabel("Generations to Stabilize");

            var averages = data.Select(g => new
            {
                Density = g.Key,
                AvgGenerations = g.Average(x => x.Generations)
            }).ToList();

            double[] avgDensities = averages.Select(x => x.Density).ToArray();
            double[] avgGenerations = averages.Select(x => x.AvgGenerations).ToArray();

            var avgLine = plot.Add.Scatter(avgDensities, avgGenerations);
            avgLine.LegendText = "Average";
            avgLine.LineWidth = 2;
            avgLine.MarkerSize = 0;

            foreach (var group in data)
            {
                double[] densities = group.Select(x => group.Key).ToArray();
                double[] generations = group.Select(x => (double)x.Generations).ToArray();

                var scatter = plot.Add.Scatter(densities, generations);
                scatter.LegendText = $"Density {group.Key.ToString(CultureInfo.InvariantCulture)}";
                scatter.MarkerSize = 5;
                scatter.MarkerShape = MarkerShape.OpenCircle;
                scatter.LineWidth = 0;
            }

            plot.ShowLegend();

            plot.SavePng("plot.png", 800, 600);
            Console.WriteLine("Graph saved as plot.png");
        }

        static void Main(string[] args)
        {
            Reset();
            LoadSettings();
            InitializeBoard();

            Console.WriteLine("Conway's Game of Life");
            Console.WriteLine("1. Randomize board");
            Console.WriteLine("2. Load pattern from file");
            Console.WriteLine("3. Save current state");
            Console.WriteLine("4. Run simulation");
            Console.WriteLine("5. Research mode");
            Console.WriteLine("6. Exit");

            while (true)
            {
                Console.Write("Select option: ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        RandomizeBoard();
                        break;
                    case "2":
                        LoadPattern();
                        break;
                    case "3":
                        SaveBoard();
                        break;
                    case "4":
                        RunSimulation();
                        break;
                    case "5":
                        ResearchMode();
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
            void LoadSettings()
            {
                try
                {
                    string json = File.ReadAllText(settingsFile);
                    settings = JsonSerializer.Deserialize<GameSettings>(json);
                }
                catch
                {
                    settings = new GameSettings();
                    SaveSettings();
                }
            }

            void SaveSettings()
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);
            }

            void InitializeBoard()
            {
                board = new Board(settings.Width, settings.Height, settings.CellSize);
            }

            void RandomizeBoard()
            {
                Console.Write("Enter alive probability (0.0-1.0, default 0.3): ");
                if (double.TryParse(Console.ReadLine(), out double probability) && probability >= 0 && probability <= 1)
                {
                    board.Randomize(probability);
                }
                else
                {
                    board.Randomize(0.3);
                }
                Render();
            }

            void LoadPattern()
            {
                if (!Directory.Exists(patternsDir))
                {
                    Directory.CreateDirectory(patternsDir);
                    Console.WriteLine($"Created '{patternsDir}' directory for pattern files.");
                    return;
                }

                var patternFiles = Directory.GetFiles("..\\..\\..\\Patterns");
                if (patternFiles.Length == 0)
                {
                    Console.WriteLine("No pattern files found in Patterns directory.");
                    return;
                }

                Console.WriteLine("Available patterns:");
                for (int i = 0; i < patternFiles.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {Path.GetFileName(patternFiles[i])}");
                }

                Console.Write("Select pattern number or enter custom file path: ");
                var input = Console.ReadLine();

                try
                {
                    if (int.TryParse(input, out int index) && index > 0 && index <= patternFiles.Length)
                    {
                        board.LoadFromFile(patternFiles[index - 1]);
                    }
                    else
                    {
                        board.LoadFromFile(input);
                    }
                    Render();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading pattern: {ex.Message}");
                }
            }

            void SaveBoard()
            {
                Console.Write("Enter file name to save: ");
                var fileName = Console.ReadLine();
                try
                {
                    if (!Directory.Exists(patternsDir))
                    {
                        Directory.CreateDirectory(patternsDir);
                    }
                    board.SaveToFile(Path.Combine(patternsDir, fileName));
                    Console.WriteLine("Board saved successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving board: {ex.Message}");
                }
            }

            void RunSimulation()
            {
                Console.WriteLine("Running simulation. Press any key to stop...");
                Render();

                while (!Console.KeyAvailable)
                {
                    board.Advance();
                    Render();
                    Thread.Sleep(settings.SimulationDelay);
                }
                Console.ReadKey(true);
            }

            void ResearchMode()
            {
                Console.WriteLine("Research Mode");
                Console.WriteLine("1. Count clusters and classify patterns");
                Console.WriteLine("2. Measure stabilization time for different densities");

                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        AnalyzeClusters();
                        break;
                    case "2":
                        MeasureStabilization();
                        PlotStabilizationGraph();
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }

            void AnalyzeClusters()
            {
                var clusters = board.FindClusters();
                Console.WriteLine($"Found {clusters.Count} clusters:");

                var classifier = new PatternClassifier();
                foreach (var cluster in clusters)
                {
                    var patternType = classifier.ClassifyPattern(cluster, board);
                    Console.WriteLine($"- Cluster with {cluster.Count} cells: {patternType}");
                }
            }

            void MeasureStabilization()
            {
                Console.WriteLine("Research densities: " + string.Join(", ", settings.ResearchDensities));
                Console.WriteLine($"Running research with {settings.ResearchIterations} iterations per density...");

                var allResults = new List<(double density, int iteration, int generations, int aliveCells)>();

                foreach (var density in settings.ResearchDensities)
                {
                    for (int i = 0; i < settings.ResearchIterations; i++)
                    {
                        board.Randomize(density);
                        var result = RunUntilStabilization();
                        allResults.Add((density, i + 1, result.generations, result.aliveCells));
                        Console.WriteLine($"Density {density.ToString(CultureInfo.InvariantCulture)}, iteration {i + 1}: stabilized after {result.generations} generations with {result.aliveCells} cells");
                    }
                }

                using (var writer = new StreamWriter("data.txt"))
                {
                    writer.WriteLine("Density\tIteration\tGenerations\tAliveCells");
                    foreach (var result in allResults)
                    {
                        writer.WriteLine($"{result.density.ToString(CultureInfo.InvariantCulture)}\t{result.iteration}\t{result.generations}\t{result.aliveCells}");
                    }
                }

                Console.WriteLine("Research completed. Data saved to data.txt");
            }


            (int generations, int aliveCells) RunUntilStabilization()
            {
                int stableGenerations = 0;
                int lastAliveCount = board.AliveCells;
                int generations = 0;

                while (stableGenerations < settings.StableGenerationsThreshold && generations < 1000)
                {
                    board.Advance();
                    generations++;

                    if (board.AliveCells == lastAliveCount)
                    {
                        stableGenerations++;
                    }
                    else
                    {
                        stableGenerations = 0;
                        lastAliveCount = board.AliveCells;
                    }
                }

                return (generations, lastAliveCount);
            }

            void Render()
            {
                Console.Clear();
                for (int row = 0; row < board.Rows; row++)
                {
                    for (int col = 0; col < board.Columns; col++)
                    {
                        var cell = board.Cells[col, row];
                        Console.Write(cell.IsAlive ? '■' : ' ');
                    }
                    Console.WriteLine();
                }
                Console.WriteLine($"Generation: {board.Generation}, Alive cells: {board.AliveCells}");
            }
        }
    }
}