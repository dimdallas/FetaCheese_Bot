using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_v6_bestMovesSet : IChessBot
{

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 320, 330, 500, 900, 10000 };
    
    int minValue = -int.MaxValue, maxValue = int.MaxValue;
    
    List<Move> sameValueMoves = new();
    Random random = new();
    
    // For Transposition Table
    // int ttCapacity = 4 * 1024 * 1024 / (sizeof(ulong) + sizeof(int));  // #DEBUG
    // Dictionary<ulong, int> transpositionTable = new(349525);
    
    // For Iterative Deepening
    Move bestMoveThisTurn, bestMoveCurrIteration;
    int bestEvaluationCurrIteration;
    // int bestEvaluationThisTurn;  // #DEBUG
    bool searchTimeout;
    // maybe add a set with best moves found to check in move order
    HashSet<Move> bestMovesSet = new();

    public Move Think(Board board, Timer timer)
    {
        // For Iterative Deepening
        // maybe safe for deleting, test, to save tokens
        bestMoveThisTurn = Move.NullMove;
        // bestEvaluationThisTurn = minValue;  // #DEBUG
        searchTimeout = false;
        bestMovesSet.Clear();
        
        // For better Memory Allocation 
        Comparison<Move> orderingComparison =
            (x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board));
        
        // Try commenting them
        GC.Collect();
        GC.WaitForPendingFinalizers();

        
        // Bot gets possible legal moves
        Span<Move> legalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref legalMoves);

        // in case of losing checks with only one move
        // Try deleting, if such low mobility probably will be checkmated
        if (legalMoves.Length == 1)
            return legalMoves[0];

        // Iterative Deepening
        // var iterationBound = IsEndgame(board) ? maxValue : 3;
        // for (var iterationDepth = 0; iterationDepth < iterationBound; iterationDepth++)
        for (var iterationDepth = 0; iterationDepth < maxValue; iterationDepth++)
        {
            bestMoveCurrIteration = Move.NullMove;
            bestEvaluationCurrIteration = minValue;
        
            
            // HERE STARTS DEPTH SEARCH
            sameValueMoves.Clear();


            // MOVE ORDERING
            // Move Ordering in root of search, good moves are search first and bad moves last
            // Optimizes alpha-beta so that solution is located on the left tree side
            legalMoves.Sort(orderingComparison);
            
            foreach (var legalMove in legalMoves)
            {
                // try isEndgame
                // var timeDifference = timer.MillisecondsRemaining - timer.OpponentMillisecondsRemaining;
                // var timeToThink = timeDifference > 0 ? Math.Min(timeDifference /2, 300) : 300;
                // var timeToThink = remaining/25 : 300;
                if (timer.MillisecondsElapsedThisTurn > 300)
                {
                    searchTimeout = true;
                    break;
                }
                
                board.MakeMove(legalMove);

                var moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, iterationDepth, orderingComparison, board);
                // Found checkmate move, stop search to find a "better" checkmate
                if (moveEvaluation == maxValue)
                {  // #DEBUG
                    /*Console.WriteLine("------CHECKMATE------" + iterationDepth);   // #DEBUG
                    Console.WriteLine("Found Checkmate, select immediately " + legalMove);  // #DEBUG
                    Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
                    Console.WriteLine("=============="+board.PlyCount+"==============");  // #DEBUG*/

                    return legalMove;
                }  // #DEBUG
                
                if (moveEvaluation > bestEvaluationCurrIteration)
                {
                    bestEvaluationCurrIteration = moveEvaluation;
                    sameValueMoves.Clear();
                }
                
                if (moveEvaluation == bestEvaluationCurrIteration)
                    sameValueMoves.Add(legalMove);

                board.UndoMove(legalMove);
            }
            
            // Bot selects a random best value move from those which came on tie
            bestMoveCurrIteration = sameValueMoves.Count >= 1
                ? sameValueMoves[random.Next(sameValueMoves.Count)]
                : Move.NullMove;
            // Maybe sameValueMoves.Sort() before selecting random
            // or DETECT REPETITION MOVE AND REMOVE IT

            if (bestMoveCurrIteration != Move.NullMove)
            {
                // Because search starts by looking previous best move, so only gets possibly beeter
                bestMoveThisTurn = bestMoveCurrIteration;
                bestMovesSet.Add(bestMoveThisTurn);
                // bestEvaluationThisTurn = bestEvaluationCurrIteration;  // #DEBUG
            }

            if (searchTimeout)
            {  // #DEBUG
                // Console.WriteLine("Reached depth " + iterationDepth);  // #DEBUG
                break;
            }  // #DEBUG
        }
        
        return bestMoveThisTurn;
    }
    
    // The main depth first search, negamax with alpha beta pruning and Qsearch
    int AlphaBetaNegamaxSearch(int alpha, int beta, int depth, Comparison<Move> orderingComparison, Board board)
    {
        if (depth == 0)
            return QuiescenceSearch(alpha, beta, orderingComparison, board);

        Span<Move> depthMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref depthMoves);
        
        // -infinity if checkmate, worst case scenario
        // 0 if isDraw or isInStalemate
        if (depthMoves.Length == 0)
            return board.IsInCheckmate() ? minValue : 0;
        
        // HERE CHECK FOR REPETITIONS
        if (board.GameRepetitionHistory.Contains(board.ZobristKey))
            // return 0;
            return Math.Min(0, beta);

        depthMoves.Sort(orderingComparison);
        
        foreach (var move in depthMoves)
        {
            board.MakeMove(move);

            var searchScore= -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, orderingComparison, board);

            board.UndoMove(move);

            //  fail hard beta-cutoff
            if (searchScore >= beta)
                return beta;

            if (searchScore > alpha)
                alpha = searchScore;
        }

        return alpha;
    }
    
    // Search all captures until a quiet position and return static evaluation
    // Delta pruning cuts Qsearch and returns backup static evaluation
    int QuiescenceSearch( int alpha, int beta, Comparison<Move> orderingComparison, Board board) {
        var stubbornScore = StaticEvaluation(board);
        
        // return stubbornScore;  // fail-soft cutoff
        if( stubbornScore >= beta )
            return beta;  // fail-hard cutoff
        
        
        // BIG DELTA Pruning, if greatest possible material swing not enough to raise alpha
        // Node is hopeless, don't generate moves
        // BigDelta equals a queen and a promotion to queen
        // var bigDelta = 2 * typeValues[(int)PieceType.Queen] - typeValues[(int)PieceType.Pawn];
        /*if (!IsEndgame(board) && stubbornScore + 1700 < alpha) // stubbornScore  < alpha - bigDelta
            return alpha;*/

        if( stubbornScore > alpha )
            alpha = stubbornScore;
        
        Span<Move> captureMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref captureMoves, true);

        if (captureMoves.Length == 0)
            return stubbornScore; // return STATIC EVALUATION

        // Move ordering, sorts depthMoves, so that good moves are search first and bad moves last
        // Optimizes Negamax and QuiescenceSearch so that solution is located on the left tree side
        captureMoves.Sort(orderingComparison);

        foreach(var captureMove in captureMoves){
            // small delta > 2 * typeValues[(int)PieceType.Pawn]
            // var delta = 200 + typeValues[(int)captureMove.CapturePieceType];
            /*if (!IsEndgame(board) && stubbornScore + 200 + typeValues[(int)captureMove.CapturePieceType] < alpha)  // stubbornScore < alpha - delta
                continue;*/

            board.MakeMove(captureMove);
            // SEE or MVV/LVA etc

            var quiesceScore = -QuiescenceSearch(-beta, -alpha, orderingComparison, board);

            board.UndoMove(captureMove);

            if( quiesceScore >= beta )
                return beta;  // fail-hard cutoff
                // return quiesceScore;  // fail-soft cutoff
                
            if( quiesceScore > alpha )
                alpha = quiesceScore;
        }
        return alpha;
    }
    
    
    int MoveOrderingEvaluation(Move move, Board board)
    {
        // Makes sure best move from previous iterative deepening is evaluated 1st
        // if (move == bestMoveThisTurn)
        if (bestMovesSet.Contains(move))
            return minValue;
        
        var evalGuess = 0;

        if (move.IsCapture) 
            evalGuess = 10 * typeValues[(int)move.CapturePieceType];

        if (move.IsPromotion)
            evalGuess += typeValues[(int)move.PromotionPieceType];

        // isEnPrise
        // this does not cover checks
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) 
            evalGuess -= typeValues[(int) move.MovePieceType];

        return -evalGuess;
    }

    // Board evaluation - Side to move relative, but final score +good and -bad 
    // MATERIAL BALANCE
    // MOP-UP EVALUATION in Endgame
    // MOBILITY EVALUATION in Endgame (on material balance/imbalance?)
    // openings bonus --> Pawn Piece Square Table ready but 100+ tokens
    // also has center control, pawn structure, king tropism, attacking king zones etc
    int StaticEvaluation(Board board)
    {
        // material evaluation
        var evaluation = GetMaterial(0, board) - GetMaterial(6, board);
        
        if (IsEndgame(board))
        {
            if (evaluation != 0)
                evaluation += MopUpEvaluation(evaluation > 0, board);
            
            // STRONGLY DOUBT about Mobility along with MopUp, see results
            // Mobility actually decreases moves to check and total ply count before repetition
            else if (board.TrySkipTurn())
            {
                Span<Move> nextPlayerMoves = stackalloc Move[256], currPlayerMoves = stackalloc Move[256];
                board.GetLegalMovesNonAlloc(ref nextPlayerMoves);
                board.UndoSkipTurn();
                board.GetLegalMovesNonAlloc(ref currPlayerMoves);
                var mobilityEvaluation = currPlayerMoves.Length - nextPlayerMoves.Length;
                // mobility evaluation
                evaluation += board.IsWhiteToMove ? mobilityEvaluation : -mobilityEvaluation;
            }
        }
        // Early game evaluation
        else
            evaluation += GetCenterControl(0, board) - GetCenterControl(6, board);
        
        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        return board.IsWhiteToMove ? evaluation : -evaluation;
    }

    // offset 0 for white, offset 6 for black
    int GetMaterial(int isWhiteOffset, Board board)
    {
        var pieceLists = board.GetAllPieceLists();
        int material = 0;
        
        // Count every piece except Kings
        for (var i = 0; i <= 4; i++)
            material += pieceLists[i+isWhiteOffset].Count * typeValues[i+1];
        
        return material;
    }
    
    // offset 0 for white, offset 6 for black
    int GetCenterControl(int isWhiteOffset, Board board)
    {
        var piecesLists = board.GetAllPieceLists();
        int mobility = 0;
        
        // reward Pawns, Knights and Bishops for center control
        for (var i = 0; i <= 2; i++)
        {
            foreach (var piece in piecesLists[i+isWhiteOffset])
            {
                mobility += 6 - CenterManhattanDistance(piece.Square);
            }
        }

        return mobility;
    }
    
    // Trap enemy king to corners, away from center
    // Reward your king to help with trapping
    //Chess 4.x --- 4.7 * CMD + 1.6 * (14 - MD)
    int MopUpEvaluation(bool whiteAdvantage, Board board)
    {
        var opponentKingSquare = board.GetKingSquare(!whiteAdvantage);

        // var trapEvaluation =/*4.7f * */CenterManhattanDistance(opponentKingSquare)
        //                                + /*1.6f * */ManhattanDistanceSubtraction(board.GetKingSquare(whiteAdvantage), opponentKingSquare)
        //                                + PieceSpecificMd(PieceType.Rook, opponentKingSquare, board, whiteAdvantage)
        //                                + PieceSpecificMd(PieceType.Queen, opponentKingSquare, board, whiteAdvantage);

        var myKingSquare = board.GetKingSquare(whiteAdvantage);
        var trapEvaluation =/*4.7f * */CenterManhattanDistance(opponentKingSquare)
                                       + /*1.6f * */(14 - Math.Abs(myKingSquare.File - opponentKingSquare.File) - Math.Abs(myKingSquare.Rank - opponentKingSquare.Rank));
        
        return whiteAdvantage ? trapEvaluation : -trapEvaluation;
    }

    // Piece distance from center Squares, d3 d4 e3 e4
    int CenterManhattanDistance(Square square)
    {
        return Math.Max(3 - square.File, square.File - 4)
               + Math.Max(3 - square.Rank, square.Rank - 4);
    }

    /*int ManhattanDistanceSubtraction(Square myPieceSquare, Square targetSquare)
    {
        return 14 - Math.Abs(myPieceSquare.File - targetSquare.File) -
               Math.Abs(myPieceSquare.Rank - targetSquare.Rank);
    }

    int PieceSpecificMd(PieceType type, Square enemyKingSquare, Board board, bool whiteAdvantage)
    {
        var myPieces = board.GetPieceList(type, whiteAdvantage);

        // maybe change this because too much heap allocation
        var reward = 0;
        foreach (var piece in myPieces) reward += ManhattanDistanceSubtraction(piece.Square, enemyKingSquare);

        return reward;
    }*/

    // Endgame position with
    // each player <==13 points in material
    // enemy king alone, insufficient material
    // pawnless game
    bool IsEndgame(Board board)
    {
        var pieces = board.GetAllPieceLists();
        int whiteMaterial = GetMaterial(0, board), blackMaterial = GetMaterial(6, board);
        return (whiteMaterial <= 1300 && blackMaterial <= 1300)
               || Math.Min(whiteMaterial, blackMaterial) == 0
               || pieces[0].Count + pieces[6].Count == 0;
    }
}