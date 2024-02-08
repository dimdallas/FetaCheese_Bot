using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot_negamax2_static : IChessBot
{
    int movesChecked;

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] typeValues = { 0, 100, 300, 325, 500, 900, 10000 };
    int minValue = -int.MaxValue;
    

    public Move Think(Board board, Timer timer)
    {
        movesChecked = 0;
        
        // Bot gets possible legal moves
        Move[] legalMoves = board.GetLegalMoves();
        List<Move> sameValueMoves = new List<Move>();

        // Move selectedMove = Move.NullMove;
        int bestEvaluation = minValue;
        
        
        // Bot checks every move with the thinking algorithm
        foreach (var legalMove in legalMoves)
        {
            // Bot searches for the direct best possible scenario regarding piece values
            // var moveEvaluation = ValueAggressiveSearch(legalMove, board);
            
            // Bot searches for the evaluation of this move given an initial depth
            board.MakeMove(legalMove);
            // depth = 0 => myBot moves evaluation, move any piece to valued position
            // depth = 1 => opponent response moves evaluation, capture opponents piece if possible
            // depth = 2 => myBot response moves to opponent responses, avoid capture or sacrifice for better capture
            // with depth = 2 (seems like some scenarios never end searching)
            // etc...
            
            var moveEvaluation = -NegamaxSearch(3, board);
            // var moveEvaluation = -AlphaBetaNegamaxSearch(minValue, maxValue, 2, board);
            // Console.WriteLine("Current eval " + moveEvaluation);
            
            if (moveEvaluation > bestEvaluation)
            {
                // selectedMove = legalMove;
                bestEvaluation = moveEvaluation;
                sameValueMoves.Clear();
                // sameValueMoves.Add(legalMove);
                // Console.WriteLine("Checked better move " + legalMove + " " + bestEvaluation);
            }
            
            if (moveEvaluation == bestEvaluation)
            {
                sameValueMoves.Add(legalMove);
                // Console.WriteLine("Checked same value move " + legalMove + " " + bestEvaluation);
            }

            movesChecked++;
            board.UndoMove(legalMove);
        }

        // Bot selects a random best value move from those which came on tie
        var selectedMove = sameValueMoves.ToArray()[GetRandomIndex(sameValueMoves.Count)];
        
        // Console.WriteLine("Same value moves " + sameValueMoves.Count);
        // Console.WriteLine("Selected " + selectedMove);
        // Console.WriteLine("Moves checked: " + movesChecked);
        // Console.WriteLine("ms " + timer.MillisecondsElapsedThisTurn);
        // Console.WriteLine("--------------"+board.PlyCount+"--------------");
        
        return selectedMove;
    }

    int GetRandomIndex(int range)
    {
        var randomMove = new Random().Next(range);
        return randomMove;
    }

    int NegamaxSearch(int depth, Board board)
    {
        if (depth == 0)
        {
            // return CaptureEvaluation(board);
            return StaticEvaluation(board);
        }

        var depthMoves = board.GetLegalMoves();
        if (depthMoves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return minValue;
            }

            //if isDraw or isInStalemate
            return 0;
        }

        var bestEvaluation = minValue;

        foreach (var move in depthMoves)
        {
            board.MakeMove(move);
            var searchEvaluation = -NegamaxSearch(depth - 1, board);
            movesChecked++;
            board.UndoMove(move);
            
            if (searchEvaluation > bestEvaluation)
            {
                bestEvaluation = searchEvaluation;
            }
        }
        return bestEvaluation;
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
        
        /*Move[] whiteMoves = board.GetLegalMoves();
        if (board.TrySkipTurn())
        {
            Move[] blackMoves = board.GetLegalMoves();
            board.UndoSkipTurn();
            mobilityEvaluation = 10 * (whiteMoves.Length - blackMoves.Length);
            Console.WriteLine("Mobility white " + whiteMoves.Length + " black " + blackMoves.Length);
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