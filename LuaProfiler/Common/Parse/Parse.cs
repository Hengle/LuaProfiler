﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UniLua;

namespace MikuLuaProfiler
{
    public static class Parse
    {
        public static readonly string LOCAL_PROFILER =
            "local BeginMikuSample = MikuLuaProfiler.LuaProfiler.BeginSample "
            + "local EndMikuSample = MikuLuaProfiler.LuaProfiler.EndSample " + "local function miku_unpack_return_value(...) "
            + "EndMikuSample()"
            + "return ... end ";
        #region parse
        public static string InsertSample(string value, string name)
        {
            LLex l = new LLex(new StringLoadInfo(value), name);
            l.InsertString(0, "BeginMikuSample(\"" + name + ", line:1 require file\") ");
            l.InsertString(0, LOCAL_PROFILER);
            int lastPos = 0;
            int nextPos = l.pos;
            l.Next();
            int tokenType = l.Token.TokenType;

            lastPos = nextPos;
            nextPos = l.pos;

            InsertSample(l, ref lastPos, ref nextPos, tokenType, false);

            return l.code;
        }

        static void InsertSample(LLex l, ref int lastPos, ref int nextPos, int tokenType, bool onlyFun)
        {
            Stack<int> tokens = new Stack<int>();

            bool needLastSample = true;
            bool hasReturn = false;
            int lastStackToken = -1;
            while (tokenType != (int)TK.EOS)
            {
                switch (tokenType)
                {
                    case (int)TK.FUNCTION:
                        hasReturn = false;
                        tokens.Push(tokenType);
                        lastStackToken = tokenType;
                        string funName = "";
                        bool isLeft = false;

                        while (tokenType != (int)TK.EOS)
                        {
                            l.Next();
                            tokenType = l.Token.TokenType;

                            lastPos = nextPos;
                            nextPos = l.pos;
                            if (!isLeft)
                            {
                                if (l.Token is NameToken)
                                {
                                    funName += ((NameToken)l.Token).SemInfo;
                                }
                                else if ((l.Token.TokenType == (int)':'))
                                {
                                    funName += ':';
                                }
                                else if ((l.Token.TokenType == (int)'.'))
                                {
                                    funName += '.';
                                }
                            }


                            if (tokenType == (int)'(')
                            {
                                isLeft = true;
                            }

                            if (tokenType == (int)')')
                            {
                                l.InsertString(nextPos - 1, " BeginMikuSample(\"" + l.Source 
                                    + ",line:" + l.LineNumber + " funName:" + funName + "\") ");
                                nextPos = l.pos;
                                break;
                            }
                        }
                        break;
                    case (int)TK.IF:
                    case (int)TK.FOR:
                    case (int)TK.WHILE:
                        if (tokens.Count > 0)
                        {
                            tokens.Push(tokenType);
                            lastStackToken = tokenType;
                        }
                        break;
                    case (int)TK.RETURN:
                        int insertPos = lastPos - 1;

                        if (tokens.Count == 0)
                        {
                            needLastSample = false;
                        }
                        Token lastTokenType = null;
                        int commentPos = insertPos;
                        while (tokenType != (int)TK.EOS)
                        {
                            if (!(lastTokenType is CommentToken)
                                && !(l.Token is SpaceToken && lastTokenType is LineToken))
                            {
                                lastTokenType = l.Token;
                            }

                            l.Next();

                            tokenType = l.Token.TokenType;
                            if (l.Token is CommentToken && !(lastTokenType is CommentToken) )
                            {
                                commentPos = nextPos - 1;
                            }

                            lastPos = nextPos;
                            nextPos = l.pos;

                            if (tokenType == (int)TK.FUNCTION)
                            {
                                InsertSample(l, ref lastPos, ref nextPos, tokenType, true);
                                tokenType = l.Token.TokenType;
                                if (l.Token is CommentToken && !(lastTokenType is CommentToken))
                                {
                                    commentPos = lastPos - 1;
                                }
                                if (!(lastTokenType is CommentToken)
                                    && !(l.Token is SpaceToken && lastTokenType is LineToken))
                                {
                                    lastTokenType = l.Token;
                                }
                            }

                            if (tokenType == (int)TK.END
                                || tokenType == (int)TK.ELSEIF
                                || tokenType == (int)TK.ELSE
                                || tokenType == (int)TK.EOS)
                            {
                                if (lastTokenType is CommentToken)
                                {
                                    lastPos = commentPos;
                                }
                                else if (lastTokenType is LineToken)
                                {
                                    lastPos = ((LineToken)lastTokenType).Pos - 1;
                                }
                                else if ((lastTokenType is LiteralToken) 
                                    && tokenType != (int)TK.EOS)
                                {
                                    lastPos = lastPos - 1;
                                }

                                string returnStr = l.ReadString(insertPos, lastPos - 1);

                                string trimValue = returnStr.Trim();
                                if (trimValue.Length > 0 && trimValue[trimValue.Length - 1] == ';')
                                {
                                    char[] value = new char[returnStr.Length - 1];
                                    bool isFront = false;
                                    for (int i = returnStr.Length - 1; i >= 0; i--)
                                    {
                                        if (returnStr[i] == ';')
                                        {
                                            isFront = true;
                                            continue;
                                        }
                                        if (!isFront)
                                        {
                                            value[i - 1] = returnStr[i];
                                        }
                                        else
                                        {
                                            value[i] = returnStr[i];
                                        }
                                    }
                                    returnStr = new string(value);
                                }
                                returnStr = " return miku_unpack_return_value(" + returnStr.Substring(6, returnStr.Length - 6) + ") ";

                                l.Replace(insertPos, lastPos - 1, returnStr);
                                nextPos = l.pos;
                                if (tokenType == (int)TK.END)
                                {
                                    if (tokens.Count > 0)
                                        tokens.Pop();
                                    if (onlyFun && tokens.Count <= 0)
                                    {
                                        l.Next();
                                        lastPos = nextPos;
                                        nextPos = l.pos;
                                        return;
                                    }
                                }
                                break;
                            }
                        }

                        if (lastStackToken != (int)TK.IF)
                        {
                            hasReturn = true;
                        }
                        break;
                    case (int)TK.END:
                        if (tokens.Count > 0)
                        {
                            int token = tokens.Pop();
                            if (token == (int)TK.FUNCTION)
                            {
                                if (!hasReturn)
                                {
                                    l.InsertString(lastPos - 1, " EndMikuSample() ");
                                    lastPos = nextPos;
                                    nextPos = l.pos;
                                }
                                if (onlyFun && tokens.Count <= 0)
                                {
                                    l.Next();
                                    lastPos = nextPos;
                                    nextPos = l.pos;
                                    return;
                                }
                            }
                            if (tokens.Count > 0)
                            {
                                var tA = tokens.ToArray();
                                lastStackToken = tA[tA.Length - 1];
                            }
                            hasReturn = false;
                        }
                        break;
                }
                l.Next();
                tokenType = l.Token.TokenType;
                lastPos = nextPos;
                nextPos = l.pos;
            }

            if (needLastSample)
            {
                l.InsertString(nextPos, " EndMikuSample()");
            }
        }
        #endregion
    }
}
#endif