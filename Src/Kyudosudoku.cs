﻿using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleSolvers;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace KyudosudokuWebsite
{
    sealed class Kyudosudoku
    {
        public const double SudokuX = 14;
        public const double SudokuY = 0;
        public const double ControlsX = 14;
        public const double ControlsY = 9.75;

        public int[][] Grids { get; private set; }
        public KyuConstraint[] Constraints { get; private set; }

        public Kyudosudoku(int[][] grids, KyuConstraint[] constraints)
        {
            if (grids == null)
                throw new ArgumentNullException(nameof(grids));
            if (grids.Length != 4)
                throw new ArgumentException("There must be four grids.", nameof(grids));
            if (grids.Any(g => g.Length != 36))
                throw new ArgumentException("The grids must all have 36 entries.", nameof(grids));

            Grids = grids;
            Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        }

        private static (int[] topLeft, int[] topRight, int[] bottomLeft, int[] bottomRight)[] GetAllKyudokuCombinations(int[] sudoku)
        {
            var emptyArray = new int[0][];
            IEnumerable<int[]> getDigits(int dx, int dy)
            {
                for (var i = 0; i < 36; i++)
                    if (sudoku[i % 6 + dx + 9 * (i / 6 + dy)] == 9)
                        for (var h = 0; h < 36; h++)
                            if (sudoku[h % 6 + dx + 9 * (h / 6 + dy)] == 8 && h % 6 != i % 6 && h / 6 != i / 6)
                                for (var g = 0; g < 36; g++)
                                    if (sudoku[g % 6 + dx + 9 * (g / 6 + dy)] == 7 && g % 6 != i % 6 && g / 6 != i / 6 && g % 6 != h % 6 && g / 6 != h / 6)
                                        for (var f = 0; f < 36; f++)
                                            if (sudoku[f % 6 + dx + 9 * (f / 6 + dy)] == 6 && f % 6 != i % 6 && f / 6 != i / 6 && f % 6 != h % 6 && f / 6 != h / 6 && f % 6 != g % 6 && f / 6 != g / 6)
                                                for (var e = 0; e < 36; e++)
                                                    if (sudoku[e % 6 + dx + 9 * (e / 6 + dy)] == 5 && e % 6 != i % 6 && e / 6 != i / 6 && e % 6 != h % 6 && e / 6 != h / 6 && e % 6 != g % 6 && e / 6 != g / 6 && e % 6 != f % 6 && e / 6 != f / 6)
                                                        for (var d = 0; d < 36; d++)
                                                            if (sudoku[d % 6 + dx + 9 * (d / 6 + dy)] == 4)
                                                                for (var c = 0; c < 36; c++)
                                                                    if (sudoku[c % 6 + dx + 9 * (c / 6 + dy)] == 3)
                                                                        for (var b = 0; b < 36; b++)
                                                                            if (sudoku[b % 6 + dx + 9 * (b / 6 + dy)] == 2)
                                                                                for (var a = 0; a < 36; a++)
                                                                                    if (sudoku[a % 6 + dx + 9 * (a / 6 + dy)] == 1)
                                                                                    {
                                                                                        var arr = new[] { a, b, c, d, e, f, g, h, i };
                                                                                        if (Enumerable.Range(0, 6).All(row => Enumerable.Range(0, 6).Sum(col => arr.Contains(col + 6 * row) ? sudoku[col + dx + 9 * (row + dy)] : 0) <= 9) &&
                                                                                            Enumerable.Range(0, 6).All(col => Enumerable.Range(0, 6).Sum(row => arr.Contains(col + 6 * row) ? sudoku[col + dx + 9 * (row + dy)] : 0) <= 9))
                                                                                        {
                                                                                            Array.Sort(arr);
                                                                                            yield return arr;
                                                                                        }
                                                                                    }
            }

            var topLefts = getDigits(0, 0).ToArray();
            var topRights = topLefts.Length == 0 ? emptyArray : getDigits(3, 0).ToArray();
            var bottomLefts = topRights.Length == 0 ? emptyArray : getDigits(0, 3).ToArray();
            var bottomRights = bottomLefts.Length == 0 ? emptyArray : getDigits(3, 3).ToArray();

            return (from k1 in topLefts from k2 in topRights from k3 in bottomLefts from k4 in bottomRights select (topLeft: k1, topRight: k2, bottomLeft: k3, bottomRight: k4)).ToArray();
        }

        public static Kyudosudoku Generate(int seed)
        {
            var lockObj = new object();
            var rnd = new Random(seed);

            tryAgain:

            // Generate a random Sudoku grid
            var sudoku = new Sudoku().Solve(new SolverInstructions { Randomizer = rnd }).First();

            // Find all possible sequences 1–9 for each quadrant that could be a valid Kyudoku solution
            var allKyudokus = GetAllKyudokuCombinations(sudoku);
            if (allKyudokus.Length == 0)
                goto tryAgain;
            allKyudokus.Shuffle(rnd);

            // Constraints that we will use to make the Sudoku unique
            var allKyConstraints = GenerateConstraints(sudoku, rnd).ToArray().Shuffle(rnd);

            // Process every combination of Kyudoku solutions to find one that leads to a valid puzzle
            for (var kyIx = 0; kyIx < allKyudokus.Length; kyIx++)
            {
                var (topLeft, topRight, bottomLeft, bottomRight) = allKyudokus[kyIx];
                var givensFromKyu = new List<GivenConstraint>();
                for (var cell = 0; cell < 81; cell++)
                    if ((cell % 9 < 6 && cell / 9 < 6 && topLeft.Contains(cell % 9 + 6 * (cell / 9))) ||
                        (cell % 9 >= 3 && cell / 9 < 6 && topRight.Contains(cell % 9 - 3 + 6 * (cell / 9))) ||
                        (cell % 9 < 6 && cell / 9 >= 3 && bottomLeft.Contains(cell % 9 + 6 * (cell / 9 - 3))) ||
                        (cell % 9 >= 3 && cell / 9 >= 3 && bottomRight.Contains(cell % 9 - 3 + 6 * (cell / 9 - 3))))
                        givensFromKyu.Add(new GivenConstraint(cell, sudoku[cell]));

                if (new Sudoku().AddConstraints(givensFromKyu, avoidColors: true).AddConstraints(allKyConstraints.Select(s => s.GetConstraint()), avoidColors: true).Solve().Take(2).Count() > 1)
                    // The Sudoku is ambiguous even with all the constraints.
                    continue;

                // Remove constraints that would be redundant. If the Sudoku was already unique to begin with, this will remove all of the constraints.
                var attempts = 3;
                tryRRagain:
                var kyConstraints = Ut.ReduceRequiredSet(
                    Enumerable.Range(0, allKyConstraints.Length),
                    state => new Sudoku().AddConstraints(givensFromKyu, avoidColors: true).AddConstraints(state.SetToTest.Select(ix => allKyConstraints[ix].GetConstraint()), avoidColors: true).Solve().Take(2).Count() == 1,
                    skipConsistencyTest: true)
                    .Select(ix => allKyConstraints[ix])
                    .ToArray();

                // Don’t allow combinations of constraints that would visually clash on the screen
                for (var i = 0; i < kyConstraints.Length; i++)
                    for (var j = i + 1; j < kyConstraints.Length; j++)
                        if (kyConstraints[i].ClashesWith(kyConstraints[j]) || kyConstraints[j].ClashesWith(kyConstraints[i]))
                        {
                            attempts--;
                            if (attempts == 0)
                                goto busted2;
                            allKyConstraints.Shuffle(rnd);
                            goto tryRRagain;
                        }

                // Try up to 10 random fillings of the Kyudoku grids. If none of these results in a valid puzzle, we start again from scratch.
                var kyudokus = new[] { topLeft, topRight, bottomLeft, bottomRight };
                for (var seed2 = 0; seed2 < 10; seed2++)
                {
                    var grids = Enumerable.Range(0, 4)
                        .Select(corner => Ut.NewArray(36, ix => kyudokus[corner].Contains(ix) ? sudoku[ix % 6 + 3 * (corner % 2) + 9 * (ix / 6 + 3 * (corner / 2))] : rnd.Next(1, 10)))
                        .ToArray();

                    // Find all possible Kyudoku solutions for the newly filled grids
                    var allSolutions = new int[4][][];
                    for (var corner = 0; corner < 4; corner++)
                    {
                        var ixs = kyudokus[corner];
                        var kyudoku = new Puzzle(36, 0, 1);
                        kyudoku.AddConstraint(new Kyudoku6x6Constraint(grids[corner]));
                        allSolutions[corner] = kyudoku.Solve().Select(solution => solution.SelectIndexWhere(v => v == 0).ToArray()).ToArray();
                        if (!allSolutions[corner].Any(solution => solution.SequenceEqual(ixs)))
                            goto busted1;
                    }

                    // Now test every combination of Kyudoku solutions to make sure that all of them result in an unsolvable Sudoku, except for one, which needs to be unique

                    var numValids = 0;
                    var numAmbiguous = 0;

                    foreach (var kys in from s1 in allSolutions[0]
                                        from s2 in allSolutions[1]
                                        from s3 in allSolutions[2]
                                        from s4 in allSolutions[3]
                                        select new[] { s1, s2, s3, s4 })
                    {
                        // Test the uniqueness of the Sudoku resulting from this combination of Kyudoku solutions
                        var sud = new Sudoku();
                        var givens = new int?[81];
                        for (var corner = 0; corner < 4; corner++)
                        {
                            foreach (var kyCell in kys[corner])
                            {
                                var sudokuCell = kyCell % 6 + 3 * (corner % 2) + 9 * (kyCell / 6 + 3 * (corner / 2));
                                // If two Kyudoku solutions transfer different digits to the same cell in the Sudoku, this combination
                                // is already invalid (which is good; we want them all to be invalid except for one)
                                if (givens[sudokuCell] != null && givens[sudokuCell] != grids[corner][kyCell])
                                    goto alright;
                                givens[sudokuCell] = grids[corner][kyCell];
                            }
                        }
                        sud.AddGivens(givens);
                        foreach (var constr in kyConstraints)
                            sud.AddConstraint(constr.GetConstraint());
                        var sols = sud.Solve().Take(2).ToArray();

                        if (sols.Length == 1)       // Sudoku is valid
                            numValids++;
                        else if (sols.Length > 1)   // Sudoku is ambiguous
                            numAmbiguous++;

                        if (numAmbiguous > 0 || numValids > 1)
                            goto busted1;
                        alright:;
                    }

                    return new Kyudosudoku(grids, kyConstraints);

                    busted1:;
                }
                busted2:;
            }
            goto tryAgain;
        }

        private static IEnumerable<KyuConstraint> GenerateConstraints(int[] sudoku, Random rnd)
        {
            var constraintGenerators = Ut.NewArray<(int num, Func<int[], IList<KyuConstraint>> generator)>(
                // Cell constraints
                (5, OddEven.Generate),
                (5, AntiBishop.Generate),
                (5, AntiKnight.Generate),
                (5, AntiKing.Generate),
                (5, NoConsecutive.Generate),

                // Row/column constraints
                (5, Sandwich.Generate),
                (5, Skyscraper.Generate),
                (5, Battlefield.Generate),
                (5, Binairo.Generate),

                // Area constraints
                (5, Thermometer.Generate),
                (5, Arrow.Generate),
                (5, Palindrome.Generate),
                (5, KillerCage.Generate),
                (5, RenbanCage.Generate),

                // Other
                (200, ConsecutiveNeighbors.Generate),
                (200, DoubleNeighbors.Generate)
            );

            foreach (var (num, generator) in constraintGenerators)
            {
                var generated = generator(sudoku);

                // Variant of Fisher-Yates shuffle that stops once we have the required number of elements
                for (int j = 0; j < generated.Count && j < num; j++)
                {
                    int item = rnd.Next(j, generated.Count);
                    if (item > j)
                    {
                        var t = generated[item];
                        generated[item] = generated[j];
                        generated[j] = t;
                    }
                    yield return generated[j];
                }
            }
        }
    }
}
