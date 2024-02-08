using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_nonAlloc : IChessBot
{
    int positionsChecked;

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 300, 325, 500, 900, 10000 };
    
    int minValue = -int.MaxValue;
    int maxValue = int.MaxValue;
    
    // List<int> orderingEvaluationGuesses = new();
    List<Move> sameValueMoves = new();

    private Dictionary<ulong, int> transpositionTable = new();
    
    private bool isEndgame;


    public Move Think(Board board, Timer timer)
    {
        positionsChecked = 0;
        sameValueMoves.Clear();

        // Bot gets possible legal moves
        // Move[] legalMoves = board.GetLegalMoves();
        Span<Move> legalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref legalMoves);
        // var legalMoves = legalMovesSpan;


        // Move Ordering in root of search, good moves are search first and bad moves last
        // Optimizes alpha-beta so that solution is located on the left tree side
        // MoveOrdering(legalMoves, board);
        legalMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));


        var bestEvaluation = minValue;
        
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

            // CHECK TRANSPOSITIONS
            // if (transpositionTable.ContainsKey(board.ZobristKey))
            // {
            //     moveEvaluation = transpositionTable[board.ZobristKey];
            // }
            // else
            // {
            //     
            // }

            if(!transpositionTable.TryGetValue(board.ZobristKey, out var moveEvaluation))
                moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, 2, board);
            
            // var moveEvaluation = -AlphaBetaNegamaxSearch(bestEvaluation, maxValue, 1, board);
            // Console.WriteLine("Current eval " + moveEvaluation);

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
            {
                sameValueMoves.Add(legalMove);
                // Console.WriteLine("Checked same move " + legalMove + " " + moveEvaluation);
            }
            /*else
            {
                Console.WriteLine("Checked WORST move " + legalMove + " " + moveEvaluation);
            }*/

            positionsChecked++;
            Console.WriteLine("Positions checked: " + positionsChecked);  // #DEBUG
            Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG
            board.UndoMove(legalMove);
        }

        // Bot selects a random best value move from those which came on tie
        var selectedMove = sameValueMoves.ToArray()[GetRandomIndex(sameValueMoves.Count)];
        
        Console.WriteLine("Same value moves " + sameValueMoves.Count);  // #DEBUG
        // Console.WriteLine("Selected " + selectedMove);  // #DEBUG
        Console.WriteLine("Moves checked: " + positionsChecked);  // #DEBUG
        Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);  // #DEBUG

        // Console.WriteLine(board.CreateDiagram(true));
        Console.WriteLine("--------------"+board.PlyCount+"--------------");  // #DEBUG
        return selectedMove;
    }

    int GetRandomIndex(int range)
    {
        var randomMove = new Random().Next(range);
        return randomMove;
    }
    
    
    int AlphaBetaNegamaxSearch(int alpha, int beta, int depth, Board board)
    {
        if (depth == 0)
        {
            // right now straight evaluations, typically second type search with evaluation
            // return StaticEvaluation(board);
            
            // Quiet positions evaluation search
            return QuiescenceSearch(alpha, beta, board);
        }

        // var depthMoves = board.GetLegalMoves();
        Span<Move> depthMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref depthMoves);
        // var depthMoves = depthMovesSpan;
        
        if (depthMoves.Length == 0)
        {
            // worst case scenario for side to move
            if (board.IsInCheckmate())
            {
                // Console.WriteLine("WCS");
                return minValue;
            }
            
            // Console.WriteLine("Draw | Stalemate");
            //if isDraw or isInStalemate
            return 0;
        }

        // MoveOrdering(depthMoves, board);
        depthMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        
        foreach (var move in depthMoves)
        {
            board.MakeMove(move);

            if (!transpositionTable.TryGetValue(board.ZobristKey, out var searchScore))
            {
                searchScore = -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, board);
                positionsChecked++;
                transpositionTable.TryAdd(board.ZobristKey, searchScore);
            }

            board.UndoMove(move);
            
            if (searchScore >= beta)
            {
                //  fail hard beta-cutoff
                return beta;
            }

            if (searchScore > alpha)
            {
                alpha = searchScore;
            }
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
        if (!isEndgame)
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
        {
            return stubbornScore;
        }
        
        // Move ordering, sorts depthMoves, so that good moves are search first and bad moves last
        // Optimizes Negamax and QuiescenceSearch so that solution is located on the left tree side
        // MoveOrdering(captureMoves, board);
        captureMoves.Sort((x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));

        foreach(var captureMove in captureMoves){
            if (!isEndgame)
            {
                var delta = 2 * typeValues[1];
                delta += typeValues[(int)captureMove.CapturePieceType];
                if (stubbornScore + delta < alpha) // stubbornScore < alpha - delta
                {
                    continue;
                }
            }

            board.MakeMove(captureMove);
            // SEE or MVV/PVA etc

            if (!transpositionTable.TryGetValue(board.ZobristKey, out var quiesceScore))
            {
                quiesceScore = -QuiescenceSearch(-beta, -alpha, board);
                positionsChecked++;
                transpositionTable.TryAdd(board.ZobristKey, quiesceScore);
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

    void MoveOrdering(Span<Move> moves, Board board)
    {
        Span<int> orderingEvaluationGuesses = stackalloc int[moves.Length];
        foreach (var unorderedMove in moves)
        {
            var evalGuess = 0;

            var movingPiece = unorderedMove.MovePieceType;

            if (unorderedMove.IsCapture)
            {
                evalGuess = 10 * typeValues[(int) movingPiece] - typeValues[(int) unorderedMove.CapturePieceType];
            }

            if (unorderedMove.IsPromotion)
            {
                evalGuess += typeValues[(int)unorderedMove.PromotionPieceType];
            }

            if (board.SquareIsAttackedByOpponent(unorderedMove.TargetSquare))
            {
                evalGuess -= typeValues[(int) movingPiece];
            }

            // orderingEvaluationGuesses[i] = evalGuess;
            // orderingEvaluationGuesses[i] = -evalGuess;
            orderingEvaluationGuesses.Fill(-evalGuess);
        }
        
        moves.Sort(orderingEvaluationGuesses);
        // Array.Sort(orderingEvaluationGuesses.ToArray(), moves);
        // Array.Reverse(moves);
    }
    
    int MoveOrderingEvaluation(Move move, Board board)
    {
        var evalGuess = 0;

        var movingPiece = move.MovePieceType;

        if (move.IsCapture)
        {
            // evalGuess = 10 * (typeValues[(int) movingPiece] - typeValues[(int) move.CapturePieceType]);
            evalGuess = 10 * typeValues[(int) movingPiece] - typeValues[(int) move.CapturePieceType];
        }

        if (move.IsPromotion)
        {
            evalGuess += typeValues[(int)move.PromotionPieceType];
        }

        if (board.SquareIsAttackedByOpponent(move.TargetSquare))
        {
            evalGuess -= typeValues[(int) movingPiece];
        }
        
        return -evalGuess;
    }

    // Board evaluation regarding piece values for each color
    // Side to move relative
    // MATERIAL BALANCE
    int StaticEvaluation(Board board)
    {
        var whoPlays = board.IsWhiteToMove;
        var pieceLists = board.GetAllPieceLists();
        int whiteMaterial = 0, blackMaterial = 0;


        for (var i = 0; i < 5; i++)
        {
            if(pieceLists[i].Count > 0)
                whiteMaterial += pieceLists[i].Count * typeValues[i + 1];
            if(pieceLists[i+6].Count > 0)
                blackMaterial += pieceLists[i+6].Count * typeValues[i + 1];
        }

        // Console.WriteLine("w "+whiteMaterial);
        // Console.WriteLine("b "+blackMaterial);
        var materialEvaluation = whiteMaterial - blackMaterial;

        var mobilityEvaluation = 0;
        
        
        // Mobility actually decreases moves to check and total ply count before repetition
        
        /*Move[] currPlayerMoves = board.GetLegalMoves();
        if (board.TrySkipTurn())
        {
            Move[] nextPlayerMoves = board.GetLegalMoves();
            board.UndoSkipTurn();
            mobilityEvaluation = 10 * (currPlayerMoves.Length - nextPlayerMoves.Length);
            // Console.WriteLine("Mobility next player " + nextPlayerMoves.Length + " curr player " + currPlayerMoves.Length);
            mobilityEvaluation *=  board.IsWhiteToMove ? 1 : -1;
        }*/

        // also has king safety, center control, pawn structure and king tropism
        var evaluation = materialEvaluation + mobilityEvaluation;

        var endgameWeight = 1f / (1 + (whoPlays ?  blackMaterial : whiteMaterial));
        // Console.WriteLine("Endgame: " + endgameWeight);

        if (isEndgame)
        {
            var trapEvaluation = TrapKingCaptureEvaluation(endgameWeight, board);
            evaluation = whoPlays ? evaluation + trapEvaluation : evaluation - trapEvaluation;
        }
        else if (endgameWeight > 0.98f)
        {
            isEndgame = true;
            // Console.WriteLine("Endgame: " + endgameWeight);  // #DEBUG
            // Console.WriteLine("b material: " + blackMaterial);
            // Console.WriteLine("a material: " + whiteMaterial);
            // Console.WriteLine(board.CreateDiagram());
        }

        // if eval > 0, white has more. if eval < 0, black has more
        // if bot plays white we want 1*eval > 0
        // if bot plays black we want -1*eval  > 0
        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        evaluation *= whoPlays ? 1 : -1;

        return evaluation;
    }

    int TrapKingCaptureEvaluation(float endgameWeight, Board board)
    {
        var trapEvaluation = 0;

        // opponent king away from the center is good evaluation
        var opponentKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
        
        var opponentRank = opponentKingSquare.Rank;
        var opponentFile = opponentKingSquare.File;

        var opponentFromCenterRank = opponentRank > 3 ? opponentRank - 4 : 3 - opponentRank;
        var opponentFromCenterFile = opponentFile > 3 ? opponentFile - 4 : 3 - opponentFile;

        var opponentKingFromCenter = opponentFromCenterFile + opponentFromCenterRank;
        trapEvaluation += opponentKingFromCenter;

        
        var myKingSquare = board.GetKingSquare(board.IsWhiteToMove);

        var myKingRank = myKingSquare.Rank;
        var myKingFile = myKingSquare.File;

        var kingsFileDst = (myKingFile - opponentFile) * (myKingFile - opponentFile > 0 ? 1 : -1);
        var kingsRankDst = (myKingRank - opponentRank) * (myKingFile - opponentFile > 0 ? 1 : -1);
        var kingsClose = kingsFileDst + kingsRankDst;
        trapEvaluation += 14 - kingsClose;
        
        
        return (int)(trapEvaluation * 10 * endgameWeight);
    }
}