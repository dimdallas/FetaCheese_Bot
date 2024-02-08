using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_v3_iterative : IChessBot
{
    int positionsChecked;  // #DEBUG
    // int transpositionsMatched;  // #DEBUG

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 320, 330, 500, 900, 10000 };
    
    int minValue = -int.MaxValue, maxValue = int.MaxValue;
    
    // List<int> orderingEvaluationGuesses = new();
    List<Move> sameValueMoves = new();
    Random random = new();

    // int ttCapacity = 4 * 1024 * 1024 / (sizeof(ulong) + sizeof(int));  // #DEBUG
    // Dictionary<ulong, int> transpositionTable = new(349525);
    
    // For Iterative Deepening
    Move bestMoveThisTurn, bestMoveCurrIteration;
    int bestEvaluationThisTurn, bestEvaluationCurrIteration;
    bool searchTimeout;

    /*public MyBot()
    {
        transpositionTable = new (ttCapacity);
        Console.WriteLine("TT capacity " + ttCapacity);  // #DEBUG
    }*/

    public Move Think(Board board, Timer timer)
    {
        positionsChecked = 0;  // #DEBUG
        // transpositionsMatched = 0;  // #DEBUG
        
        
        // For Iterative Deepening
        bestMoveThisTurn = Move.NullMove;
        bestEvaluationThisTurn = minValue;
        searchTimeout = false;
        
        GC.Collect();
        GC.WaitForPendingFinalizers();

        
        // Bot gets possible legal moves
        Span<Move> legalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref legalMoves);

        if (legalMoves.Length == 1)
        {  // #DEBUG
            // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
            //
            // // Console.WriteLine(board.GetFenString());  // #DEBUG
            // Console.WriteLine("--------------"+board.PlyCount+"--------------");  // #DEBUG
        
            return legalMoves.ToArray()[0];
        }  // #DEBUG
        
        // Iterative Deepening
        for (int iterationDepth = 0; iterationDepth < maxValue; iterationDepth++)
        {
            bestMoveCurrIteration = Move.NullMove;
            bestEvaluationCurrIteration = minValue;
        
            
            // HERE STARTS DEPTH SEARCH
            // try do negamax directly here rather than DepthSearch
            // DepthSearch(iterationDepth, legalMoves, board, timer);

            sameValueMoves.Clear();
            // Move Ordering in root of search, good moves are search first and bad moves last
            // Optimizes alpha-beta so that solution is located on the left tree side
            // MoveOrdering
            legalMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
            
            foreach (var legalMove in legalMoves)
            {
                if (timer.MillisecondsElapsedThisTurn > 300)
                {
                    searchTimeout = true;
                    break;
                }
                board.MakeMove(legalMove);
                // depth = 0 => myBot moves evaluation, move any piece to valued position
                // depth = 1 => opponent response moves evaluation, capture opponents piece if possible
                // depth = 2 => myBot response moves to opponent responses, avoid capture or sacrifice for better capture
                // etc...

                //maybe try get from TT
                var moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, iterationDepth, board);
                positionsChecked++; // #DEBUG
                
                if (moveEvaluation > bestEvaluationCurrIteration)
                {
                    bestEvaluationCurrIteration = moveEvaluation;
                    sameValueMoves.Clear();
                }
                
                if (moveEvaluation == bestEvaluationCurrIteration)
                    sameValueMoves.Add(legalMove);

                // Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
                // Console.WriteLine("---Legal move " + legalMove);  // #DEBUG
                // Console.WriteLine("Evaluation " + moveEvaluation);  // #DEBUG
                // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
                board.UndoMove(legalMove);
            }
            
            // Bot selects a random best value move from those which came on tie
            bestMoveCurrIteration = sameValueMoves.Count >= 1
                ? sameValueMoves.ToArray()[random.Next(sameValueMoves.Count)]
                : Move.NullMove;
            // Maybe sameValueMoves.Sort() before selecting random
            
            // Console.WriteLine("------Iteration " + iterationDepth);   // #DEBUG
            // if (sameValueMoves.Count > 1) // #DEBUG
            // { // #DEBUG
            //     // Console.WriteLine("Same value moves " + sameValueMoves.Count); // #DEBUG
            //     foreach (var move in sameValueMoves) // #DEBUG
            //     {
            //         // #DEBUG
            //         Console.WriteLine(move); // #DEBUG
            //     } // #DEBUG
            // } // #DEBUG

            // Console.WriteLine("Selected " + bestMoveCurrIteration);  // #DEBUG
            // Console.WriteLine("Evaluation " + bestEvaluationCurrIteration);  // #DEBUG
            // Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
            // Console.WriteLine("Search Timeout: " + searchTimeout);  // #DEBUG
            
            if (bestMoveCurrIteration != Move.NullMove)
            {
                // Because search starts by looking previous best move, so only gets better possibly
                bestMoveThisTurn = bestMoveCurrIteration;
                bestEvaluationThisTurn = bestEvaluationCurrIteration;
            }
            // else  // #DEBUG
            //     Console.WriteLine("Iteration interrupted before legal moves");  // #DEBUG


            if (searchTimeout)
            {  // #DEBUG
                // Console.WriteLine("Reached depth " + iterationDepth);  // #DEBUG
                break;
            }  // #DEBUG
        }
        
        // Console.WriteLine("------End DepthSearch");  // #DEBUG
        // Console.WriteLine("bestMoveThisTurn " + bestMoveThisTurn);  // #DEBUG
        // Console.WriteLine("bestEvaluationThisTurn " + bestEvaluationThisTurn);  // #DEBUG
        /*if (IsEndgame(board))  // #DEBUG
        {   // #DEBUG
            Console.WriteLine("Transpositions matched: " + transpositionsMatched); // #DEBUG
            // Console.WriteLine("Transpositions added: " + transpositionTable.Count); // #DEBUG
            Console.WriteLine("TT occupancy " + transpositionTable.Count / (float)ttCapacity); // #DEBUG
        }   // #DEBUG*/
        
        // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG

        // Console.WriteLine(board.GetFenString());  // #DEBUG
        // Console.WriteLine("--------------"+board.PlyCount+"--------------");  // #DEBUG
        
        return bestMoveThisTurn;
    }

    /*void DepthSearch(int iterationDepth, Span<Move> legalMoves, Board board, Timer timer)
    {
        sameValueMoves.Clear();
        // Move Ordering in root of search, good moves are search first and bad moves last
        // Optimizes alpha-beta so that solution is located on the left tree side
        // MoveOrdering
        legalMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        
        var moveEvaluation = minValue;
        
        // Bot checks every move with the thinking algorithm
        foreach (var legalMove in legalMoves)
        {
            if (timer.MillisecondsElapsedThisTurn > 300)
            {
                searchTimeout = true;
                break;
            }
            board.MakeMove(legalMove);
            // depth = 0 => myBot moves evaluation, move any piece to valued position
            // depth = 1 => opponent response moves evaluation, capture opponents piece if possible
            // depth = 2 => myBot response moves to opponent responses, avoid capture or sacrifice for better capture
            // with depth = 2 (seems like some scenarios never end searching)
            // etc...

            //maybe try get from TT
            moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, iterationDepth, board);
            positionsChecked++; // #DEBUG
            

            if (moveEvaluation > bestEvaluationCurrIteration)
            {
                bestEvaluationCurrIteration = moveEvaluation;
                // bestEvaluationThisTurn = moveEvaluation;
                sameValueMoves.Clear();
            }
            
            if (moveEvaluation == bestEvaluationCurrIteration)
                sameValueMoves.Add(legalMove);

            // Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
            // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
            board.UndoMove(legalMove);
        }

        //ERROR WHEN KING CHECKED
        // Bot selects a random best value move from those which came on tie
        // var selectedMove = sameValueMoves.ToArray()[random.Next(sameValueMoves.Count)];
        bestMoveCurrIteration = sameValueMoves.Count >= 1
            ? sameValueMoves.ToArray()[random.Next(sameValueMoves.Count)]
            : Move.NullMove;
        // Maybe sameValueMoves.Sort() before selecting random
        
        Console.WriteLine("------Iteration " + iterationDepth);   // #DEBUG
        if (sameValueMoves.Count > 1) // #DEBUG
        { // #DEBUG
            Console.WriteLine("Same value moves " + sameValueMoves.Count); // #DEBUG
            foreach (var move in sameValueMoves) // #DEBUG
            {
                // #DEBUG
                Console.WriteLine(move); // #DEBUG
            } // #DEBUG
        } // #DEBUG

        Console.WriteLine("Selected " + bestMoveCurrIteration);  // #DEBUG
        Console.WriteLine("Evaluation " + bestEvaluationCurrIteration);  // #DEBUG
        // Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
        // Console.WriteLine("Search Timeout: " + searchTimeout);  // #DEBUG
    }*/
    
    int AlphaBetaNegamaxSearch(int alpha, int beta, int depth, Board board)
    {
        if (depth == 0)
            return QuiescenceSearch(alpha, beta, board);  // alternative is Static Eval

        Span<Move> depthMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref depthMoves);
        
        if (depthMoves.Length == 0)
            // -infinity if checkmate, worst case scenario
            // 0 if isDraw or isInStalemate
            return board.IsInCheckmate() ? minValue : 0;

        depthMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        
        foreach (var move in depthMoves)
        {
            board.MakeMove(move);

            var searchScore= -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, board);
                positionsChecked++;  // #DEBUG

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
            return stubbornScore; // return STATIC EVALUATION

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

            var quiesceScore = -QuiescenceSearch(-beta, -alpha, board);
            positionsChecked++;  // #DEBUG

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
    // use  GetMaterial. GetMobility
    // use PawnPST openings
    int MoveOrderingEvaluation(Move move, Board board)
    {
        // Makes sure best move from previous iterative deepening is evaluated 1st
        if (move == bestMoveThisTurn)
            return minValue;
        
        var evalGuess = 0;

        var movingPiece = (int) move.MovePieceType;

        if (move.IsCapture) 
            evalGuess = 10 * typeValues[(int)move.CapturePieceType] - typeValues[movingPiece];

        // maybe passed pawns
        /*if (movingPiece == 1  && IsEndgame(board))
            evalGuess += 200;*/
        // if (move.MovePieceType == PieceType.Pawn)
        // if (movingPiece == 1)
        //     evalGuess += move.TargetSquare.Rank;

        //if (move.MovePieceType == PieceType.King && board.PlyCount <= 10)
        //    evalGuess -= 500;

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
    // also has king safety, center control, pawn structure and king tropism
    int StaticEvaluation(Board board)
    {
        var whoPlays = board.IsWhiteToMove;
        
        // material evaluation
        var evaluation = GetMaterial(true, board) - GetMaterial(false, board);


        if (IsEndgame(board))
        {
            if (evaluation != 0)
            {
                evaluation += MopupEvaluation(evaluation > 0, board);
            }

            if (board.IsInCheck())
            {
                evaluation += whoPlays ? 1000 : -1000;
            }
        }
        // else
        // {
        //     // check openings
        //     evaluation += GetPST(board, PieceType.Pawn, whoPlays);
        // }
        
        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        evaluation *= whoPlays ? 1 : -1;
        
        // Mobility actually decreases moves to check and total ply count before repetition
        // TRY MOBILITY WEIGHT = 5 OR 2 OR 1
        if (/*evaluation > 0 && */ board.TrySkipTurn())
        {
            var opponentMoves = board.GetLegalMoves().Length;
            board.UndoSkipTurn();
            // mobility evaluation
            // var myPseudoLegalNextTurn = board.GetLegalMoves().Length;
            evaluation += board.GetLegalMoves().Length - opponentMoves;
        }
        // evaluation += GetMobilityMyView(board);
        
        return evaluation;
    }

    int GetMaterial(bool isWhite, Board board)
    {
        var pieceLists = board.GetAllPieceLists();
        int material = 0, offset = isWhite ? 0 : 6;
        
        for (var i = 0; i < 5; i++)
            if(pieceLists[i+offset].Count > 0)
                material += pieceLists[i+offset].Count * typeValues[i+1];
        

        return material;
    }
    
    /*int GetMobilityMyView(Board board)
    {
        
        // var mobilityWeight = 10;
        // TRY MOBILITY WEIGHT = 5 OR 2 OR 1
        if (!board.TrySkipTurn()) return 0;
        
        var opponentMoves = board.GetLegalMoves().Length;
        board.UndoSkipTurn();

        // mobility evaluation
        // return (board.GetLegalMoves().Length - opponentMoves) * (board.IsWhiteToMove ? 1 : -1);
        return board.GetLegalMoves().Length - opponentMoves;
    }*/
    
    /*int GetMobilityCorrected(bool white, Board board)
    {
        
        // var mobilityWeight = 10;
        // TRY MOBILITY WEIGHT = 5 OR 2 OR 1
        if (!board.TrySkipTurn()) return 0;
        
        var opponentMoves = board.GetLegalMoves().Length;
        board.UndoSkipTurn();
        var myPseudoLegalMoves = board.GetLegalMoves().Length;

        // mobility evaluation
        // return (board.GetLegalMoves().Length - opponentMoves) * (board.IsWhiteToMove ? 1 : -1);
        return white ? myPseudoLegalMoves - opponentMoves : opponentMoves - myPseudoLegalMoves;
    }*/

    int MopupEvaluation(bool whiteAdvantage, Board board)
    {
        // opponent king away from the center is good evaluation
        var opponentKingSquare = board.GetKingSquare(!whiteAdvantage);
        
        var myKingSquare = board.GetKingSquare(whiteAdvantage);

        //Chess 4.x --- 4.7 * CMD + 1.6 * (14 - MD)
        var trapEvaluation =/*4.7f * */(Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File -4)
                                        + Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank -4)) 
                                       + /*1.6f * */(14 - Math.Abs(myKingSquare.File - opponentKingSquare.File) - Math.Abs(myKingSquare.Rank - opponentKingSquare.Rank));

        return whiteAdvantage ? trapEvaluation : -trapEvaluation;
    }
    
    /*int ManhattanDistanceSubtraction(Square myPieceSquare, Square enemyKingSquare)
    {
        return 14 - Math.Abs(myPieceSquare.File - enemyKingSquare.File) -
               Math.Abs(myPieceSquare.Rank - enemyKingSquare.Rank);
    }

    int PieceSpecificMD(PieceType type, Square enemyKingSquare, Board board, bool whiteAdvantage)
    {
        var eval = 0;
        var myPieces = board.GetPieceList(type, whiteAdvantage);
        foreach (var piece in myPieces)
        {
            eval += ManhattanDistanceSubtraction(piece.Square, enemyKingSquare);
        }

        return eval;
    }*/

    // Piece-Square Tables
    /*int GetPST(Board board, PieceType type, bool isWhite)
    {
        var pieces = board.GetPieceList(type, isWhite);
        int[] pawnPst =
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
            5, 5, 10, 25, 25, 10, 5, 5,
            0, 0, 0, 20, 20, 0, 0, 0,
            5, -5, -10, 0, 0, -10, -5, 5,
            5, 10, 10, -20, -20, 10, 10, 5,
            0, 0, 0, 0, 0, 0, 0, 0
        };
        var evaluationPst = 0;
        foreach (var piece in pieces)
        {
            evaluationPst += isWhite ? pawnPst[8*(7-piece.Square.Rank)+ piece.Square.File] : pawnPst[8*piece.Square.Rank+ 7 - piece.Square.File];
        }
        return evaluationPst;
    }*/
    
    // int KingTropism(Board board)
    // {
    //     return 0;
    // }
    //
    // int AttackingKingZone(Board board)
    // {
    //     return 0;
    // }

    bool IsEndgame(Board board)
    {
        // Endgame position with each player <==13 points in material
        return (GetMaterial(true, board) <= 1300 || GetMaterial(false, board) <= 1300) || IsPawnlessEndgame(board);
    }
    
    
    // check again for mop-up evaluation in static
    bool IsPawnlessEndgame(Board board)
    {
        return board.GetAllPieceLists()[0].Count + board.GetAllPieceLists()[6].Count == 0 && !board.IsInsufficientMaterial();
    }
}