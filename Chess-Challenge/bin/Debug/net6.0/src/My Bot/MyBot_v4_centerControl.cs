using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_v4_centerControl : IChessBot
{
    // int positionsChecked;  // #DEBUG
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
    // maybe add a set with best moves found to check in move order
    // HashSet<Move> bestMovesSet = new();

    public Move Think(Board board, Timer timer)
    {
        // positionsChecked = 0;  // #DEBUG
        // transpositionsMatched = 0;  // #DEBUG
        
        
        // For Iterative Deepening
        bestMoveThisTurn = Move.NullMove;
        bestEvaluationThisTurn = minValue;  // #DEBUG
        searchTimeout = false;
        // bestMovesSet.Clear();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();

        
        // Bot gets possible legal moves
        Span<Move> legalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref legalMoves);

        // in case of losing checks
        if (legalMoves.Length == 1)
            return legalMoves[0];

        // Iterative Deepening
        // try iterationDepth < isEndgame() ? naxValue : 3
        for (int iterationDepth = 0; iterationDepth < maxValue; iterationDepth++)
        {
            bestMoveCurrIteration = Move.NullMove;
            bestEvaluationCurrIteration = minValue;
        
            
            // HERE STARTS DEPTH SEARCH
            sameValueMoves.Clear();
            // Move Ordering in root of search, good moves are search first and bad moves last
            // Optimizes alpha-beta so that solution is located on the left tree side
            // MoveOrdering
            legalMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
            
            foreach (var legalMove in legalMoves)
            {
                // try isEndgame
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
                // positionsChecked++; // #DEBUG
                
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
            
            
            if (bestMoveCurrIteration != Move.NullMove)
            {
                // Because search starts by looking previous best move, so only gets better possibly
                bestMoveThisTurn = bestMoveCurrIteration;
                // bestMovesSet.Add(bestMoveThisTurn);
                bestEvaluationThisTurn = bestEvaluationCurrIteration;  // #DEBUG
            }

            if (searchTimeout)
            {  // #DEBUG
                break;
            }  // #DEBUG
        }
        
        return bestMoveThisTurn;
    }
    
    // The main depth first search, negamax with alpha beta pruning and Qsearch
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
            /*if (board.GameRepetitionHistory.Contains(board.ZobristKey))
                searchScore -= minValue / 2;*/
                // positionsChecked++;  // #DEBUG

            board.UndoMove(move);
            
            if (searchScore >= beta)
                //  fail hard beta-cutoff
                return beta;

            if (searchScore > alpha)
                alpha = searchScore;
        }

        return alpha;
    }
    
    // Search all captures until a quiet position and return static evaluation
    // Delta pruning cuts Qsearch and returns backup static evaluation
    int QuiescenceSearch( int alpha, int beta, Board board) {
        var stubbornScore = StaticEvaluation(board);
        
        // return stubbornScore;  // fail-soft cutoff
        if( stubbornScore >= beta )
            return beta;  // fail-hard cutoff
        
        // BIG DELTA Pruning, if greatest possible material swing not enough to raise alpha
        // Node is hopeless, don't generate moves
        if (!IsEndgame(board))
        {
            // var bigDelta = 2 * typeValues[5] - 100;  // BigDelta equals a queen and a promotion to queen
            if (stubbornScore + 1700 < alpha)   // stubbornScore  < alpha - bigDelta
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
                // var delta = 2 * typeValues[1];
                // small delta = 2 pawns
                // var delta = 200 + typeValues[(int)captureMove.CapturePieceType];
                if (stubbornScore + 200 + typeValues[(int)captureMove.CapturePieceType] < alpha) // stubbornScore < alpha - delta
                    continue;
            }

            board.MakeMove(captureMove);
            // SEE or MVV/LVA etc

            var quiesceScore = -QuiescenceSearch(-beta, -alpha, board);
            // positionsChecked++;  // #DEBUG

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
    int MoveOrderingEvaluation(Move move, Board board)
    {
        // Makes sure best move from previous iterative deepening is evaluated 1st
        if (move == bestMoveThisTurn)
        // if (bestMovesSet.Contains(move))
            return minValue;
        
        var evalGuess = 0;

        var movingPiece = (int) move.MovePieceType;

        if (move.IsCapture) 
            evalGuess = 10 * typeValues[(int)move.CapturePieceType]/* - typeValues[movingPiece]*/;

        if (move.IsPromotion)
            evalGuess += typeValues[(int)move.PromotionPieceType];

        // isEnPrise
        // this does not cover checks
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) 
            evalGuess -= typeValues[movingPiece];
        
        // maybe passed pawns
        /*if (movingPiece == 1  && IsEndgame(board))
            evalGuess += 200;*/
        // if (movingPiece == 1)
        //     evalGuess += move.TargetSquare.Rank;


        return -evalGuess;
    }

    // Board evaluation - Side to move relative
    // MATERIAL BALANCE
    // MOP-UP EVALUATION in Endgame and Pawnless endgame
    // Mobility evaluation?
    // openings bonus --> Pawn Piece Square Table ready but 100+ tokens
    // also has center control, pawn structure, king tropism, attacking king zones etc
    int StaticEvaluation(Board board)
    {
        // var whoPlays = board.IsWhiteToMove;
        
        // material evaluation
        var evaluation = GetMaterial(true, board) - GetMaterial(false, board);



        if (IsEndgame(board))
        {
            if (evaluation != 0)
                evaluation += MopUpEvaluation(evaluation > 0, board);
        }
        else
            evaluation += GetCenterControl(true, board) - GetCenterControl(false, board);
        
        // Mobility actually decreases moves to check and total ply count before repetition
        // TRY MOBILITY WEIGHT = 5 OR 2 OR 1
        /*if (/*evaluation > 0 && #1# /*IsEndgame(board) &&#1# board.TrySkipTurn())
        {
            var opponentMoves = board.GetLegalMoves().Length;
            board.UndoSkipTurn();
            // mobility evaluation
            // var myPseudoLegalNextTurn = board.GetLegalMoves().Length;
            evaluation += board.GetLegalMoves().Length - opponentMoves;
        }*/
        
        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        // evaluation *= whoPlays ? 1 : -1;
        // return evaluation;
        return board.IsWhiteToMove ? evaluation : -evaluation;
    }

    int GetMaterial(bool isWhite, Board board)
    {
        var pieceLists = board.GetAllPieceLists();
        int material = 0, offset = isWhite ? 0 : 6;
        
        for (var i = 0; i < 5; i++)
            // if(pieceLists[i+offset].Count > 0)
            material += pieceLists[i+offset].Count * typeValues[i+1];
        
        return material;
    }
    
    int GetCenterControl(bool isWhite, Board board)
    {
        var piecesLists = board.GetAllPieceLists();
        int mobility = 0, offset = isWhite ? 0 : 6;
        // var mobilityWeight = 10;
        // TRY MOBILITY WEIGHT = 5 OR 2 OR 1
        
        for (var i = 0; i < 4; i++)
        {
            foreach (var piece in piecesLists[i+offset])
            {
                mobility += 6 - CenterManhattanDistance(piece.Square);
            }
        }

        return mobility;
    }
    
    int MopUpEvaluation(bool whiteAdvantage, Board board)
    {
        // opponent king away from the center is good evaluation
        var opponentKingSquare = board.GetKingSquare(!whiteAdvantage);
        
        //Chess 4.x --- 4.7 * CMD + 1.6 * (14 - MD)
        var trapEvaluation =/*4.7f * */CenterManhattanDistance(opponentKingSquare)
                                       + /*1.6f * */ManhattanDistanceSubtraction(board.GetKingSquare(whiteAdvantage), opponentKingSquare)
                                       + PieceSpecificMd(PieceType.Rook, opponentKingSquare, board, whiteAdvantage)
                                       + PieceSpecificMd(PieceType.Queen, opponentKingSquare, board, whiteAdvantage);

        return whiteAdvantage ? trapEvaluation : -trapEvaluation;
    }

    int CenterManhattanDistance(Square square)
    {
        return Math.Max(3 - square.File, square.File - 4)
               + Math.Max(3 - square.Rank, square.Rank - 4);
    }

    int ManhattanDistanceSubtraction(Square myPieceSquare, Square targetSquare)
    {
        return 14 - Math.Abs(myPieceSquare.File - targetSquare.File) -
               Math.Abs(myPieceSquare.Rank - targetSquare.Rank);
    }

    int PieceSpecificMd(PieceType type, Square enemyKingSquare, Board board, bool whiteAdvantage)
    {
        var myPieces = board.GetPieceList(type, whiteAdvantage);

        // maybe change this because too much heap allocation
        var sum = 0;
        foreach (var piece in myPieces) sum += ManhattanDistanceSubtraction(piece.Square, enemyKingSquare);

        return sum;
    }
    
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
        // CHECK AGAIN && or ||
        return (GetMaterial(true, board) <= 1300 && GetMaterial(false, board) <= 1300)
               || board.GetAllPieceLists()[0].Count + board.GetAllPieceLists()[6].Count == 0;
    }
}