using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_ab3_ordering_static_trapKing : IChessBot
{
    int movesChecked;

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 300, 325, 500, 900, 10000 };
    int minValue = -int.MaxValue;
    int maxValue = int.MaxValue;

    List<int> orderingEvaluationGuesses = new();
    List<Move> sameValueMoves = new();

    public Move Think(Board board, Timer timer)
    {
        movesChecked = 0;
        sameValueMoves.Clear();
        
        // Bot gets possible legal moves
        Move[] legalMoves = board.GetLegalMoves();
        
        // Move Ordering in root of search, good moves are search first and bad moves last
        // Optimizes alpha-beta so that solution is located on the left tree side
        // Array.Sort(legalMoves, (x, y) => MoveOrderingEvaluation(x, board).CompareTo(MoveOrderingEvaluation(y, board)));
        MoveOrdering(legalMoves, board);

        

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

            // var moveEvaluation = -AlphaBetaNegamaxSearch(bestEvaluation, maxValue, 1, board);
            var moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, 2, board);
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

            movesChecked++;
            board.UndoMove(legalMove);
        }

        // Bot selects a random best value move from those which came on tie
        var selectedMove = sameValueMoves.ToArray()[GetRandomIndex(sameValueMoves.Count)];
        
        // Console.WriteLine("Same value moves " + sameValueMoves.Count);
        // Console.WriteLine("Selected " + selectedMove);
        // Console.WriteLine("Moves checked: " + movesChecked);
        // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);

        // Console.WriteLine(board.CreateDiagram(true));
        // Console.WriteLine("--------------"+board.PlyCount+"--------------");
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
            return StaticEvaluation(board);
            
            // Quiet positions evaluation search
            // return QuiescenceSearch(alpha, beta, board);
        }

        
        // Span<Move> depthMovesSpan = stackalloc Move[256];
        // board.GetLegalMovesNonAlloc(ref depthMovesSpan);
        // var depthMoves = depthMovesSpan.ToArray();
        var depthMoves = board.GetLegalMoves();
        
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

        foreach (var move in depthMoves)
        {
            board.MakeMove(move);
            var searchScore = -AlphaBetaNegamaxSearch(-beta, -alpha, depth - 1, board);
            movesChecked++;
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

    void MoveOrdering(Move[] moves, Board board)
    {
        orderingEvaluationGuesses.Clear();
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
            orderingEvaluationGuesses.Add(-evalGuess);
        }
        
        Array.Sort(orderingEvaluationGuesses.ToArray(), moves);
        // Array.Reverse(moves);
    }

    // Board evaluation regarding piece values for each color
    // Side to move relative
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
        
        // Check strategies
        /*if (board.IsInCheckmate())
        {
            evaluation += board.IsWhiteToMove ? 3000 : -3000;
        
        }
        else if (board.IsInCheck())
        {
            evaluation += board.IsWhiteToMove ? 1000 : -1000;
        }*/

        var endgameWeight = 1f / (1 + (whoPlays ?  blackMaterial : whiteMaterial));
        // Console.WriteLine("Endgame: " + endgameWeight);

        if (endgameWeight > 0.98f)
        {
            // Console.WriteLine("Endgame: " + endgameWeight);

            var trapEvaluation = TrapKingCaptureEvaluation(endgameWeight, board);
            evaluation = whoPlays ? evaluation + trapEvaluation : evaluation - trapEvaluation;
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
        var kingsRankDst = (myKingRank - opponentRank) * (myKingRank - opponentRank > 0 ? 1 : -1);
        var kingsClose = kingsFileDst + kingsRankDst;
        trapEvaluation += 14 - kingsClose;
        
        
        return (int)(trapEvaluation * 10 * endgameWeight);
    }
}