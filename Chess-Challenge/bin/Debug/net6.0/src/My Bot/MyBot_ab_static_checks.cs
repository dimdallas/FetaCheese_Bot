using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_ab_static_checks : IChessBot
{
    int movesChecked;

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 300, 325, 500, 900, 10000 };
    int minValue = -int.MaxValue;
    int maxValue = int.MaxValue;


    public Move Think(Board board, Timer timer)
    {
        movesChecked = 0;
        
        // Bot gets possible legal moves
        Move[] legalMoves = board.GetLegalMoves();
        List<Move> sameValueMoves = new List<Move>();

        int alphaLower = minValue;
        
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

            var moveEvaluation = -AlphaBetaNegamaxSearch(alphaLower, maxValue, 2, board);
            // Console.WriteLine("Current eval " + moveEvaluation);

            // if (legalMove.MovePieceType == PieceType.King && board.PlyCount <= 10)
            // {
            //     moveEvaluation -= 500;
            // }

            if (moveEvaluation > alphaLower)
            {
                // selectedMove = legalMove;
                alphaLower = moveEvaluation;
                sameValueMoves.Clear();
                // sameValueMoves.Add(legalMove);
                // Console.WriteLine("Checked better move " + legalMove + " " + bestEvaluation);
            }
            
            if (moveEvaluation == alphaLower)
            {
                sameValueMoves.Add(legalMove);
                Console.WriteLine("Checked same move " + legalMove + " " + alphaLower);
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
        
        Console.WriteLine("Same value moves " + sameValueMoves.Count);
        Console.WriteLine("Selected " + selectedMove);
        Console.WriteLine("Moves checked: " + movesChecked);
        Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);
        Console.WriteLine("--------------"+board.PlyCount+"--------------");
        
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

        var depthMoves = board.GetLegalMoves();
        if (depthMoves.Length == 0)
        {
            // worst case scenario for side to move
            if (board.IsInCheckmate())
            {
                Console.WriteLine("WCS");
                return minValue;
            }
            
            Console.WriteLine("Draw | Stalemate");
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

    // Board evaluation regarding piece values for each color
    // Side to move relative
    int StaticEvaluation(Board board)
    {
        var pieceLists = board.GetAllPieceLists();
        // var whitePieces = new PieceList[5];
        // var blackPieces = new PieceList[5];
        // Array.Copy(pieceLists, 0, whitePieces, 0, 6);
        // Array.Copy(pieceLists, 6, blackPieces, 0, 6);
        
        // wKing and bKing scores cancel out because never captured
        var whiteMaterial = pieceLists[0].Count * 100 + pieceLists[1].Count * 300 + pieceLists[2].Count * 325
                            + pieceLists[3].Count * 500 + pieceLists[4].Count * 900;
        var blackMaterial = pieceLists[6].Count * 100 + pieceLists[7].Count * 300 + pieceLists[8].Count * 325
                            + pieceLists[9].Count * 500 + pieceLists[10].Count * 900;

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
        
        
        // if eval > 0, white has more. if eval < 0, black has more
        // if bot plays white we want 1*eval > 0
        // if bot plays black we want -1*eval  > 0
        // Evaluation does not care for what bot plays because we assume opponent makes the best move
        evaluation *= board.IsWhiteToMove ? 1 : -1;
        
        if (board.IsInCheckmate())
        {
            evaluation += board.IsWhiteToMove ? 3000 : -3000;
        
        }
        else if (board.IsInCheck())
        {
            evaluation += board.IsWhiteToMove ? 1000 : -1000;
        }

        return evaluation;
    }
}