using cli_life;
using Xunit;

namespace Life.Tests
{
    public class CellTests
    {
        [Fact]
        public void Cell_StartsDead()
        {
            var cell = new Cell();
            Assert.False(cell.IsAlive);
        }

        [Fact]
        public void DetermineNextLiveState_DeadWith3Neighbors_ComesAlive()
        {
            var cell = new Cell { IsAlive = false };
            AddLiveNeighbors(cell, 3);
            cell.DetermineNextLiveState();
            Assert.True(cell.IsAliveNext);
        }

        [Fact]
        public void DetermineNextLiveState_AliveWith2Neighbors_StaysAlive()
        {
            var cell = new Cell { IsAlive = true };
            AddLiveNeighbors(cell, 2);
            cell.DetermineNextLiveState();
            Assert.True(cell.IsAliveNext);
        }

        [Fact]
        public void DetermineNextLiveState_AliveWith4Neighbors_Dies()
        {
            var cell = new Cell { IsAlive = true };
            AddLiveNeighbors(cell, 4);
            cell.DetermineNextLiveState();
            Assert.False(cell.IsAliveNext);
        }

        private void AddLiveNeighbors(Cell cell, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var neighbor = new Cell { IsAlive = true };
                cell.neighbors.Add(neighbor);
            }
        }

        [Fact]
        public void AliveCells_AfterRandomize_MatchesActualCount()
        {
            var board = new Board(100, 100, 1);
            board.Randomize(0.3);

            int manualCount = 0;
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                    if (board.Cells[x, y].IsAlive)
                        manualCount++;

            Assert.Equal(manualCount, board.AliveCells);
        }
    }

    public class BoardTests
    {
        [Fact]
        public void Board_InitializesWithCorrectDimensions()
        {
            var board = new Board(100, 50, 1);
            Assert.Equal(100, board.Columns);
            Assert.Equal(50, board.Rows);
        }

        [Fact]
        public void Randomize_SetsApproximateLiveDensity()
        {
            var board = new Board(100, 100, 1);
            board.Randomize(0.3);
            double density = (double)board.AliveCells / (board.Columns * board.Rows);
            Assert.InRange(density, 0.25, 0.35);
        }

        [Fact]
        public void Advance_IncrementsGeneration()
        {
            var board = new Board(10, 10, 1);
            int initialGen = board.Generation;
            board.Advance();
            Assert.Equal(initialGen + 1, board.Generation);
        }

        [Fact]
        public void ConnectNeighbors_CreatesToroidalTopology()
        {
            var board = new Board(3, 3, 1);
            var cornerCell = board.Cells[0, 0];
            Assert.Contains(board.Cells[2, 2], cornerCell.neighbors);
        }

        [Fact]
        public void SaveAndLoad_File_PreservesState()
        {
            var board1 = new Board(10, 10, 1);
            board1.Randomize(0.5);
            board1.SaveToFile("test_board.txt");

            var board2 = new Board(10, 10, 1);
            board2.LoadFromFile("test_board.txt");

            for (int x = 0; x < board1.Columns; x++)
            {
                for (int y = 0; y < board1.Rows; y++)
                {
                    Assert.Equal(board1.Cells[x, y].IsAlive, board2.Cells[x, y].IsAlive);
                }
            }

            File.Delete("test_board.txt");
        }
    }

    public class PatternClassifierTests
    {
        private readonly PatternClassifier _classifier = new PatternClassifier();

        [Fact]
        public void ClassifyPattern_RecognizesBlock()
        {
            var cluster = new HashSet<(int, int)> {
                (0, 0), (0, 1), (1, 0), (1, 1)
            };
            var board = new Board(3, 3, 1);
            var result = _classifier.ClassifyPattern(cluster, board);
            Assert.Equal("Block (Still Life)", result);
        }

        [Fact]
        public void ClassifyPattern_RecognizesBlinker()
        {
            var cluster = new HashSet<(int, int)> {
                (1, 0), (1, 1), (1, 2)
            };
            var board = new Board(3, 3, 1);
            var result = _classifier.ClassifyPattern(cluster, board);
            Assert.Equal("Blinker (Oscillator)", result);
        }

        [Fact]
        public void ClassifyPattern_RecognizesGlider()
        {
            var cluster = new HashSet<(int, int)> {
                (0, 2), (1, 0), (1, 2), (2, 1), (2, 2)
            };
            var board = new Board(4, 4, 1);
            var result = _classifier.ClassifyPattern(cluster, board);
            Assert.Equal("Glider (Spaceship)", result);
        }

        [Fact]
        public void ClassifyPattern_RecognizesBeehive()
        {
            var cluster = new HashSet<(int, int)> {
                (1, 0), (2, 0),
                (0, 1), (3, 1),
                (1, 2), (2, 2)
            };
            var board = new Board(5, 5, 1);
            var result = _classifier.ClassifyPattern(cluster, board);
            Assert.Equal("Beehive (Still Life)", result);
        }

        [Fact]
        public void ClassifyPattern_ReturnsUnknownForRandomPattern()
        {
            var cluster = new HashSet<(int, int)> {
                (0, 0), (2, 2), (4, 4)
            };
            var board = new Board(5, 5, 1);
            var result = _classifier.ClassifyPattern(cluster, board);
            Assert.Equal("Unknown pattern", result);
        }
    }
}