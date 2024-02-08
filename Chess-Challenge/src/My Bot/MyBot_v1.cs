using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_v1 : IChessBot
{
    int positionsChecked;  // #DEBUG
    int transpositionsMatched;  // #DEBUG

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 300, 325, 500, 900, 10000 };
    
    int minValue = -int.MaxValue, maxValue = int.MaxValue;
    
    // List<int> orderingEvaluationGuesses = new();
    List<Move> sameValueMoves = new();
    Random random = new();

    int ttCapacity = 4 * 1024 * 1024 / (sizeof(ulong) + sizeof(int));  // #DEBUG
    Dictionary<ulong, int> transpositionTable = new(349525);

    /*public MyBot()
    {
        transpositionTable = new (ttCapacity);
        Console.WriteLine("TT capacity " + ttCapacity);  // #DEBUG
    }*/


    public Move Think(Board board, Timer timer)
    {
        positionsChecked = 0;  // #DEBUG
        transpositionsMatched = 0;  // #DEBUG
        sameValueMoves.Clear();

        //  for Iterative deepening move ordering
        Move bestMove;  
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        

        // Bot gets possible legal moves
        // Move[] legalMoves = board.GetLegalMoves();
        Span<Move> legalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref legalMoves);
        // Span<Move> sameValueMoves = stackalloc Move[legalMoves.Length];


        // Move Ordering in root of search, good moves are search first and bad moves last
        // Optimizes alpha-beta so that solution is located on the left tree side
        // MoveOrdering
        legalMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        
        var bestEvaluation = minValue;
        var moveEvaluation = minValue;
        
        // Bot checks every move with the thinking algorithm
        foreach (var legalMove in legalMoves)
        {
            // Bot searches for the evaluation of this move given an initial depth
            board.MakeMove(legalMove);
            // depth = 0 => myBot moves evaluation, move any piece to valued position
            // depth = 1 => opponent response moves evaluation, capture opponents piece if possible
            // depth = 2 => myBot response moves to opponent responses, avoid capture or sacrifice for better capture
            // with depth = 2 (seems like some scenarios never end searching)
            // etc...

            if (IsEndgame(board))
            {
                // CHECK TRANSPOSITIONS
                if (!transpositionTable.TryGetValue(board.ZobristKey, out moveEvaluation))
                {
                    moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, 2, board);
                    positionsChecked++; // #DEBUG
                    // revise when adding transpositions, possibly deeper than root if endgame
                    transpositionTable.TryAdd(board.ZobristKey, moveEvaluation);
                }
                else  // #DEBUG
                {  // #DEBUG
                    transpositionsMatched++; // #DEBUG
                    /*Console.WriteLine(board.CreateDiagram()); // #DEBUG
                    Console.WriteLine("From Transposition table " + board.ZobristKey); // #DEBUG
                    Console.WriteLine("From main value " + moveEvaluation); // #DEBUG*/
                }  // #DEBUG
            }
            else
            {  // #DEBUG
                moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, 2, board);
                positionsChecked++; // #DEBUG
                // Console.WriteLine("Current eval " + moveEvaluation);
            }  // #DEBUG
            

            // if (legalMove.MovePieceType == PieceType.King && board.PlyCount <= 10)
            // {
            //     moveEvaluation -= 500;
            // }

            if (moveEvaluation > bestEvaluation)
            {
                // selectedMove = legalMove;
                bestEvaluation = moveEvaluation;
                sameValueMoves.Clear();
                // sameValueMoves.Add(legalMove);
                // Console.WriteLine("Checked better move " + legalMove + " " + moveEvaluation);
            }
            
            if (moveEvaluation == bestEvaluation)
                sameValueMoves.Add(legalMove);

            // Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
            // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
            board.UndoMove(legalMove);
        }

        // Bot selects a random best value move from those which came on tie
        // var selectedMove = sameValueMoves.ToArray()[GetRandomIndex(sameValueMoves.Count)];
        var selectedMove = sameValueMoves.ToArray()[random.Next(sameValueMoves.Count)];
        
        // Console.WriteLine("Same value moves " + sameValueMoves.Count);  // #DEBUG
        /*foreach (var move in sameValueMoves)  // #DEBUG
        {  // #DEBUG
            Console.WriteLine(move);  // #DEBUG
        }  // #DEBUG
        Console.WriteLine("Selected " + selectedMove);  // #DEBUG
        Console.WriteLine("Evaluation " + bestEvaluation);  // #DEBUG*/
        /*Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
        if (IsEndgame(board))  // #DEBUG
        {   // #DEBUG
            Console.WriteLine("Transpositions matched: " + transpositionsMatched); // #DEBUG
            // Console.WriteLine("Transpositions added: " + transpositionTable.Count); // #DEBUG
            Console.WriteLine("TT occupancy " + transpositionTable.Count / (float)ttCapacity); // #DEBUG
        }   // #DEBUG

        Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG

        // Console.WriteLine(board.GetFenString());  // #DEBUG
        Console.WriteLine("--------------"+board.PlyCount+"--------------");*/  // #DEBUG
        
        return selectedMove;
    }

    /*int GetRandomIndex(int range)
    {
        var randomMove = new Random().Next(range);
        return randomMove;
    }*/
    
    
    int AlphaBetaNegamaxSearch(int alpha, int beta, int depth, Board board)
    {
        if (depth == 0)
            return QuiescenceSearch(alpha, beta, board);  // alternative is Static Eval

        // var depthMoves = board.GetLegalMoves();
        Span<Move> depthMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref depthMoves);
        // var depthMoves = depthMovesSpan;
        
        if (depthMoves.Length == 0)
        {
            /*// worst case scenario for side to move
            if (board.IsInCheckmate())
            {
                // Console.WriteLine("WCS");
                return minValue;
            }
            
            // Console.WriteLine("Draw | Stalemate");
            
            return 0;*/

            // -infinity if checkmate, worst case scenario
            // 0 if isDraw or isInStalemate
            return board.IsInCheckmate() ? minValue : 0;
        }

        // MoveOrdering(depthMoves, board);
        depthMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        
        foreach (var move in depthMoves)
        {
            board.MakeMove(move);

            var searchScore = minValue;
            if (IsEndgame(board))
            {
                // WITH TRANSPOSITIONS TABLE
                if (!transpositionTable.TryGetValue(board.ZobristKey, out searchScore))
                {
                    searchScore = -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, board);
                    positionsChecked++; // #DEBUG
                    // revise when adding transposition, probably not here, only deeper positions
                    transpositionTable.TryAdd(board.ZobristKey, searchScore);
                }
                else  // #DEBUG
                {  // #DEBUG
                    transpositionsMatched++; // #DEBUG
                    /*Console.WriteLine(board.CreateDiagram()); // #DEBUG
                    Console.WriteLine("From ABNegamax Transposition table " + board.ZobristKey); // #DEBUG
                    Console.WriteLine("From ABNegamax value " + searchScore); // #DEBUG*/
                }  // #DEBUG
            }
            else
            {  // #DEBUG
                searchScore = -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, board);
                positionsChecked++;  // #DEBUG
            }  // #DEBUG

            board.UndoMove(move);
            
            if (searchScore >= beta)
                //  fail hard beta-cutoff
                return beta;

            if (searchScore > alpha)
                alpha = searchScore;
        }

        return alpha;
    }
    
    // Search all captures and have static evaluation as a backup
    int QuiescenceSearch( int alpha, int beta, Board board) {
        var stubbornScore = StaticEvaluation(board);
        
        // return stubbornScore;  // fail-soft cutoff
        if( stubbornScore >= beta )
            return beta;  // fail-hard cutoff
        
        // BIG DELTA Pruning, if greatest possible material swing not enough to raise alpha
        // Node is hopeless, don't generate moves
        if (!IsEndgame(board))
        {
            var bigDelta = typeValues[5];
            if (stubbornScore + bigDelta < alpha)
                return alpha;
        }

        if( stubbornScore > alpha )
            alpha = stubbornScore;

        // var captureMoves = board.GetLegalMoves(true);
        Span<Move> captureMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref captureMoves, true);
        // var captureMoves = captureMovesSpan;

        if (captureMoves.Length == 0)
            return stubbornScore;  // return STATIC EVALUATION

        // Move ordering, sorts depthMoves, so that good moves are search first and bad moves last
        // Optimizes Negamax and QuiescenceSearch so that solution is located on the left tree side
        // MoveOrdering(captureMoves, board);
        captureMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));

        foreach(var captureMove in captureMoves){
            if (!IsEndgame(board))
            {
                var delta = 2 * typeValues[1];
                delta += typeValues[(int)captureMove.CapturePieceType];
                if (stubbornScore + delta < alpha) // stubbornScore < alpha - delta
                    continue;
            }

            board.MakeMove(captureMove);
            // SEE or MVV/PVA etc

            int quiesceScore;

            if (IsEndgame(board))
            {
                // WITH TRANSPOSITION TABLE
                if (!transpositionTable.TryGetValue(board.ZobristKey, out quiesceScore))
                {
                    quiesceScore = -QuiescenceSearch(-beta, -alpha, board);
                    positionsChecked++;  // #DEBUG
                    // MAYBE add only here, after qsearch returns from STATIC EVAL
                    transpositionTable.TryAdd(board.ZobristKey, quiesceScore);  // revise when you add transposition
                }
                else  // #DEBUG
                {  // #DEBUG
                    transpositionsMatched++; // #DEBUG
                    /*Console.WriteLine(board.CreateDiagram()); // #DEBUG
                    Console.WriteLine("From QSearch Transposition table " + board.ZobristKey); // #DEBUG
                    Console.WriteLine("From QSearch value " + searchScore); // #DEBUG*/
                }  // #DEBUG
            }
            else
            {
                quiesceScore = -QuiescenceSearch(-beta, -alpha, board);
                positionsChecked++;  // #DEBUG
            }

            board.UndoMove(captureMove);

            if( quiesceScore >= beta )
                return beta;  // fail-hard cutoff
                // return quiesceScore;  // fail-soft cutoff
                
            if( quiesceScore > alpha )
                alpha = quiesceScore;
        }
        return alpha;
    }
    
    
    // Improve move ordering
    // Use transposition values if existing
    // Use best move in iterative deepening
    // openings
    int MoveOrderingEvaluation(Move move, Board board)
    {
        var evalGuess = 0;

        var movingPiece = (int) move.MovePieceType;

        if (move.IsCapture) 
            evalGuess = 10 * typeValues[(int)move.CapturePieceType] - typeValues[movingPiece];

        /*if (movingPiece == 1 &&  && IsEndgame(board))
        {
            evalGuess += 200;
        }*/

        if (move.IsPromotion) 
            evalGuess += typeValues[(int)move.PromotionPieceType];

        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) 
            evalGuess -= typeValues[movingPiece];

        return -evalGuess;
    }

    // Board evaluation - Side to move relative
    // MATERIAL BALANCE
    // Mobility evaluation
    // Mop-up evaluation in Pawnless endgames
    // openings bonus
    int StaticEvaluation(Board board)
    {
        var whoPlays = board.IsWhiteToMove;
        
        // material evaluation
        var evaluation = GetMaterial(board, true) - GetMaterial(board, false);
        
        // Mobility actually decreases moves to check and total ply count before repetition
        // var mobilityWeight = 10;
        /*var currPlayerMoves = board.GetLegalMoves().Length;
        if (board.TrySkipTurn())
        {
            var nextPlayerMoves = board.GetLegalMoves().Length;
            board.UndoSkipTurn();
            // mobility evaluation
            evaluation += 10 * (whoPlays ? currPlayerMoves - nextPlayerMoves : nextPlayerMoves - currPlayerMoves);
            // Console.WriteLine("Mobility next player " + nextPlayerMoves.Length + " curr player " + currPlayerMoves.Length);
        }*/

        // also has king safety, center control, pawn structure and king tropism
        // var evaluation = materialEvaluation + mobilityEvaluation;

        // var endgameWeight = 1f / (1 + (whoPlays ?  blackMaterial : whiteMaterial));
        // Console.WriteLine("Endgame: " + endgameWeight);
        
        
        // check again
        // if (IsPawnlessEndgame(board))
        if (IsEndgame(board))
        {
            // Console.WriteLine("In Mopup evaluation");  // #DEBUG
            // Console.WriteLine("w mat " + GetMaterial(board, true));  // #DEBUG
            // Console.WriteLine("b mat " + GetMaterial(board, false));  // #DEBUG
            var trapEvaluation = MopupEvaluation(board);
            evaluation += whoPlays ? trapEvaluation : -trapEvaluation;
        }
        /*else
        {
            // check openings
        }*/

        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        // evaluation *= whoPlays ? 1 : -1;

        return whoPlays ? evaluation : -evaluation;
    }

    int MopupEvaluation(Board board)
    {
        // opponent king away from the center is good evaluation
        var opponentKingSquare = board.GetKingSquare(board.IsWhiteToMove);
        
        var opponentRank = opponentKingSquare.Rank;
        var opponentFile = opponentKingSquare.File;

        var opponentFromCenterRank = opponentRank > 3 ? opponentRank - 4 : 3 - opponentRank;
        var opponentFromCenterFile = opponentFile > 3 ? opponentFile - 4 : 3 - opponentFile;

        var opponentKingFromCenter = opponentFromCenterFile + opponentFromCenterRank;
        //Center Manhattan distance
        var trapEvaluation = 4.7f * opponentKingFromCenter;

        
        var myKingSquare = board.GetKingSquare(!board.IsWhiteToMove);

        var myKingRank = myKingSquare.Rank;
        var myKingFile = myKingSquare.File;

        var kingsFileDst = (myKingFile - opponentFile) * (myKingFile - opponentFile > 0 ? 1 : -1);
        var kingsRankDst = (myKingRank - opponentRank) * (myKingRank - opponentRank > 0 ? 1 : -1);
        var kingsClose = kingsFileDst + kingsRankDst;
        trapEvaluation += 1.6f * (14 - kingsClose);
        
        //Chess 4.x --- 4.7 * CMD + 1.6 * (14 - MD)
        return (int)trapEvaluation;
    }

    int GetMaterial(Board board, bool white)
    {
        var pieceLists = board.GetAllPieceLists();
        int material = 0, offset = white ? 0 : 6;
        
        for (var i = 0; i < 5; i++)
            if(pieceLists[i+offset].Count > 0)
                material += pieceLists[i+offset].Count * typeValues[i+1];
        

        return material;
    }

    bool IsEndgame(Board board)
    {
        // Endgame position with each player 13>== points in material
        return GetMaterial(board, true) <= 1300 && GetMaterial(board, false) <= 1300;
        
        
        // DIDN'T WORK
        // Endgame position having 4>== pieces excluding kings and pawns
        /*var pieces = 0;
        var pieceLists = board.GetAllPieceLists();
        for (int i = 1; i < 5; i++)
        {
            pieces += pieceLists[i].Count + pieceLists[i + 6].Count;
        }
        return pieces <= 4;*/
    }
    
    
    // check again for mop-up evaluation in static
    /*bool IsPawnlessEndgame(Board board)
    {
        return board.GetAllPieceLists()[0].Count + board.GetAllPieceLists()[6].Count == 0;
    }*/
}