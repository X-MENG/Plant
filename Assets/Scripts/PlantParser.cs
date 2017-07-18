using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MiniJSON;

public enum CondOp
{
    OR,
    AND,
}

public enum FormulaType
{
    FT_NUM,     // 数字
    FT_STR,     // 字符串
    FT_EXP,     // 表达式
    FT_FUN,     // 函数
}

public class FormulaFunc
{
    public FormulaFunc()
    {
        FuncName = "";
        Type = FormulaType.FT_NUM;
        args = new List<string>();
    }

    public int GetArgIndex(string arg)
    {
        int index = -1;
        for(int i = 0; i < args.Count; ++i)
        {
            if(args[i] == arg)
            {
                index = i;
                break;
            }
        }

        return index;
    }

    public string ToStr()
    {
        string funcStr = "";
        funcStr += FuncName;
        funcStr += " ";
        funcStr += "(";

        for(int i = 0; i < args.Count; ++i)
        {
            funcStr += " ";
            funcStr += args[i].ToString();
            if(i != args.Count - 1)
            {
                funcStr += ",";
            }
        }

        funcStr += " ";
        funcStr += ")";

        return funcStr;
    }

    public string FuncName;
    public FormulaType Type;
    public List<string> args;
}

public class FormulaNode
{
    public FormulaNode()
    {
        Node = "";
        Replaced = false;
        Type = FormulaType.FT_STR;
    }

    public FormulaType Type;
    public string Node;
    public bool Replaced;
}

public struct RulePair
{
    public RulePair(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key;
    public string Value;
}

public class ReplaceStr
{
    public ReplaceStr()
    {
        Mid = "";
        Prev = "";
        Next = "";
    }

    public string Mid;
    public string Prev;
    public string Next;
}

public class PlantParser
{
    public void Init(string cfgName)
    {
        string cfgStr = Util.ReadStreamingAsset(cfgName);
        mPlantCfg = Json.Deserialize(cfgStr) as Dictionary<string, object>;

        if (mPlantCfg.ContainsKey("repeat_count") == true)
        {
            mRepeatCount = int.Parse(mPlantCfg["repeat_count"].ToString());
        }

        mIgnoreList = new List<string>();
        if(mPlantCfg.ContainsKey("ignore") == true)
        {
            string ignoreStr = mPlantCfg["ignore"].ToString();
            string[] ignoreArr = ignoreStr.Split(' ');
            foreach(string s in ignoreArr)
            {
                mIgnoreList.Add(s);
            }
        }

        if (mPlantCfg.ContainsKey("type") == true)
        {
            mType = mPlantCfg["type"].ToString();
        }

        if (mPlantCfg.ContainsKey("formula") == true)
        {
            mFormula = mPlantCfg["formula"].ToString();
        }

        if (mPlantCfg.ContainsKey("turn_angle") == true)
        {
            mTurnAngle = float.Parse(mPlantCfg["turn_angle"].ToString());
        }

        mProb = new List<object>();
        if (mPlantCfg.ContainsKey("prob") == true)
        {
            mProb = mPlantCfg["prob"] as List<object>;
        }

        mRule = new List<RulePair>();
        if (mPlantCfg.ContainsKey("rule") == true)
        {
            List<object> ruleList = mPlantCfg["rule"] as List<object>;
            foreach (List<object> item in ruleList)
            {
                RulePair rp = new RulePair(item[0].ToString(), item[1].ToString());
                mRule.Add(rp);
            }
        }

        if(mType == "seq")
        {
            for (int i = 0; i < mRepeatCount; ++i)
            {
                UpdateFormulaWithSeq();
            }
            Util.DBG("formula = " + mFormula);
        }
        else if(mType == "rand")
        {
            for(int i = 0; i < mRepeatCount; ++i)
            {
                UpdateFormulaWithRand();
            }
            Util.DBG("formula = " + mFormula);
        }
    }

    private bool CalcCondition(FormulaFunc formulaFunc, FormulaFunc ruleFunc, string condStr)
    {
        string[] condArr = condStr.Split(' ');

        bool result = true;
        CondOp curOP = CondOp.AND;

        for(int i = 0; i < condArr.Length; ++i)
        {
            int deepCount = 0;
            if(condArr[i] == "(")
            {
                ++deepCount;
            }
            else if(condArr[i] == ")")
            {
                --deepCount;
            }
            else if(condArr[i] == "&")
            {
                curOP = CondOp.AND;
            }
            else if(condArr[i] == "|")
            {
                curOP = CondOp.OR;
            }
            else if(condArr[i] == "==" 
                || condArr[i] == "!=" 
                || condArr[i] == ">"
                || condArr[i] == "<"
                || condArr[i] == ">="
                || condArr[i] == "<=")
            {
                string left  = condArr[i - 1];
                float right = float.Parse(condArr[i + 1]);
                int index = ruleFunc.GetArgIndex(left);
                float param = float.Parse(formulaFunc.args[index]);

                if(condArr[i] == "==")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param == right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param == right);
                    }
                }
                else if(condArr[i] == "!=")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param != right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param != right);
                    }
                }
                else if(condArr[i] == ">")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param > right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param > right);
                    }
                }
                else if(condArr[i] == "<")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param < right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param < right);
                    }
                }
                else if(condArr[i] == ">=")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param >= right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param >= right);
                    }
                }
                else if(condArr[i] == "<=")
                {
                    if(curOP == CondOp.AND)
                    {
                        result = result && (param <= right);
                    }
                    else if(curOP == CondOp.OR)
                    {
                        result = result || (param <= right);
                    }
                }
            }
        }

        return result;
    }

    private FormulaFunc MakeFunByStr(string funStr)
    {
        FormulaFunc funType = new FormulaFunc();
        string[] fun = funStr.Split(' ');
        funType.FuncName = fun[0];
        
        int paramStartIndex = 0;
        int paramEndIndex = 0;

        for(int i = 1; i < fun.Length; ++i)
        {
            if(fun[i] == "(" && (i + 1  < fun.Length))
            {
                paramStartIndex = i + 1;
                break;
            }
        }

        funType.Type = FormulaType.FT_NUM;

        for(int i = fun.Length - 1; i >= 0; --i)
        {
            if(fun[i] == ")" && (i - 1 >= 0))
            {
                paramEndIndex = i - 1;
                break;
            }
        }

        string paramStr = "";
        for(int i = paramStartIndex; i <= paramEndIndex; ++i)
        {
            if (paramStr == "")
            {
                paramStr = fun[i];
            }
            else
            {
                paramStr += " ";
                paramStr += fun[i];
            }
        }

        string[] paramList = paramStr.Split(',');

        for(int i = 0; i < paramList.Length; ++i)
        {
            string[] ss = paramList[i].Split(' ');
            string exp = BuildDotStr(ss);
            funType.args.Add(ToPoExp(exp));
        }

        return funType;
    }

    private ReplaceStr BuildReplaceStr(string key)
    {
        ReplaceStr rs = new ReplaceStr();

        if (key.Contains(" ") == true)
        {
            string[] keyArr = key.Split(' ');

            int step = 0;

            if ((key.Contains("<") == false && key.Contains(">") == true) || (key.Contains("<") == false && key.Contains(">") == false))
            {
                ++step;
            }

            foreach (string keyItem in keyArr)
            {
                if (keyItem == "<")
                {
                    ++step;
                }
                else if (keyItem == ">")
                {
                    ++step;
                }
                else
                {
                    if (step == 0)
                    {
                        if (rs.Prev == "")
                        {
                            rs.Prev = keyItem;
                        }
                        else
                        {
                            Util.ERR("error in prev node(only one node accept)");
                        }
                    }
                    else if (step == 1)
                    {
                        if (rs.Mid == "")
                        {
                            rs.Mid = keyItem;
                        }
                        else
                        {
                            Util.ERR("error in mid node(only one node accept)");
                        }
                    }
                    else if (step == 2)
                    {
                        if (rs.Next == "")
                        {
                            rs.Next = keyItem;
                        }
                        else
                        {
                            Util.ERR("error in next node(only one node accept)");
                        }
                    }
                }
            }
        }
        else
        {
            rs.Mid = key;
        }

        return rs;
    }

    private List<FormulaNode> CreateFormulaNodeList(string formulaStr)
    {
        List<FormulaNode> formulaNodeList = new List<FormulaNode>();
        string[] formulaArr = formulaStr.Split(' ');

        for(int i = 0; i < formulaArr.Length;)
        {
            string item = formulaArr[i];
            if (item == "(")
            {
                if (i - 1 < 0)
                {
                    Util.DBG("wrong formula item");
                    break;
                }
                else
                {
                    int deepCount = 1;
                    int j = i + 1;
                    string funName = formulaNodeList[formulaNodeList.Count - 1].Node;
                    string p = item;
                    while(j < formulaArr.Length)
                    {
                        if(formulaArr[j] == "(")
                        {
                            p += " ";
                            p += formulaArr[j];
                            ++deepCount;
                        }
                        else if(formulaArr[j] == ")")
                        {
                            p += " ";
                            p += formulaArr[j];
                            --deepCount;
                            if(deepCount == 0)
                            {
                                break;
                            }
                        }
                        else
                        {
                            p += " ";
                            p += formulaArr[j];
                        }
                        ++j;
                    }
                    string fullName = funName + " " + p;
                    formulaNodeList[formulaNodeList.Count - 1].Node = fullName;
                    formulaNodeList[formulaNodeList.Count - 1].Type = FormulaType.FT_FUN;
                    string[] ff = fullName.Split(' ');
                    i += ff.Length - 1;
                }
            }
            else
            {
                FormulaNode node = new FormulaNode();
                node.Node = item;
                formulaNodeList.Add(node);
                ++i;
            }
        }

        return formulaNodeList;
    }

    private void InsertSection(RulePair rp, int idx, List<FormulaNode> formulaList)
    {
        string[] rv = rp.Value.Split(' ');
        int curIndex = idx;
        for (int i = 0; i < rv.Length; ++i)
        {
            string rr = rv[i];
            if (i == 0)
            {
                formulaList[curIndex].Node = rr;
                formulaList[curIndex].Replaced = true;
            }
            else
            {
                FormulaNode n = new FormulaNode();
                n.Node = rr;
                n.Replaced = true;
                formulaList.Insert(curIndex + 1, n);
                ++curIndex;
            }
        }
    }

    private void DoReplaceStr(RulePair rp, List<FormulaNode> formulaList, int i)
    {
        ReplaceStr rs = BuildReplaceStr(rp.Key);

        if (formulaList[i].Node == "+"
            || formulaList[i].Node == "-"
            || formulaList[i].Node == "["
            || formulaList[i].Node == "]")
        {
            return;
        }
        else
        {
            if (formulaList[i].Replaced == true)
            {
                return;
            }

            if (formulaList[i].Node == rs.Mid)
            {
                if (rs.Prev == "" && rs.Next == "")
                {
                    InsertSection(rp, i, formulaList);
                }
                else if (rs.Prev != "" && rs.Next == "")
                {
                    if (GetPrevNode(formulaList, i) == rs.Prev)
                    {
                        InsertSection(rp, i, formulaList);
                    }
                }
                else if (rs.Prev == "" && rs.Next != "")
                {
                    if (GetNextNode(formulaList, i) == rs.Next)
                    {
                        InsertSection(rp, i, formulaList);
                    }
                }
                else if (rs.Prev != "" && rs.Next != "")
                {
                    if (GetNextNode(formulaList, i) == rs.Next && GetPrevNode(formulaList, i) == rs.Prev)
                    {
                        InsertSection(rp, i, formulaList);
                    }
                }
            }
        }
    }

    private string BuildNonDotStr(string str)
    {
        string[] s = str.Split(' ');
        string nonDotStr = "";
        for (int i = 0; i < s.Length; ++i)
        {
            nonDotStr += s[i];
        }

        return nonDotStr;
    }

    private string BuildDotStr(string[] strArr)
    {
        string retStr = "";
        for(int i = 0; i < strArr.Length; ++i)
        {
            if(strArr[i] != "")
            {
                if(retStr == "")
                {
                    retStr = strArr[i];
                }
                else
                {
                    retStr += " ";
                    retStr += strArr[i];
                }
            }
        }

        return retStr;
    }

    private void DoReplaceFun(RulePair rp, List<FormulaNode> formulaList, int idx)
    {
        if(formulaList[idx].Replaced == true)
        {
            return;
        }

        if (rp.Key.Contains(" ") == true)
        {
            string[] splitStr = rp.Key.Split(':');
            string ruleKeyFunStr = splitStr[0];
            string ruleKeyCondStr = splitStr[1];

            string[] ruleKeyFuncArr = ruleKeyFunStr.Split(' ');
            string ruleKeyFuncDotStr = BuildDotStr(ruleKeyFuncArr);

            FormulaFunc formulaFunc = MakeFunByStr(formulaList[idx].Node);

            // 解析函数
            FormulaFunc ruleFunc = MakeFunByStr(ruleKeyFuncDotStr);

            // 解析条件
            string[] ruleKeyCondArr = ruleKeyCondStr.Split(' ');
            string ruleKeyCondDotStr = BuildDotStr(ruleKeyCondArr);
            if (formulaFunc.FuncName == ruleFunc.FuncName && CalcCondition(formulaFunc, ruleFunc, ruleKeyCondDotStr) == true)
            {
                List<FormulaNode> ruleFormulaNodeList = CreateFormulaNodeList(rp.Value);
                for (int i = 0; i < ruleFormulaNodeList.Count; ++i)
                {
                    if (ruleFormulaNodeList[i].Type == FormulaType.FT_FUN)
                    {
                        FormulaFunc f = MakeFunByStr(ruleFormulaNodeList[i].Node);
                        ruleFormulaNodeList[i].Node = f.ToStr();
                    }
                }
                ReplaceFormulaFunNode(formulaList, idx, ruleFunc, ruleFormulaNodeList);
            }
        }
    }

    private void ReplaceFormulaFunNode(List<FormulaNode> formulaList, int i, FormulaFunc ruleFunc, List<FormulaNode> ruleFormulaNodeList)
    {
        FormulaFunc formulaFunc = MakeFunByStr(formulaList[i].Node);
        if (formulaFunc.FuncName != ruleFunc.FuncName)
        {
            return;
        }

        List<FormulaNode> needInsertNodes = new List<FormulaNode>();
        // 执行替换操作
        for(int j = 0; j < ruleFormulaNodeList.Count; ++j)
        {
            FormulaNode node = ruleFormulaNodeList[j];
            if(node.Type == FormulaType.FT_FUN)
            {
                FormulaFunc valueFunc = MakeFunByStr(node.Node);
                FormulaFunc resultFunc = CalcPoExp(formulaFunc, ruleFunc, valueFunc);
                FormulaNode insertNode = new FormulaNode();
                insertNode.Type = FormulaType.FT_FUN;
                insertNode.Node = resultFunc.ToStr();
                insertNode.Replaced = true;
                needInsertNodes.Add(insertNode);
            }
            else if(node.Type == FormulaType.FT_STR)
            {
                FormulaNode insertNode = new FormulaNode();
                insertNode.Type = FormulaType.FT_STR;
                insertNode.Node = node.Node;
                insertNode.Replaced = true;
                needInsertNodes.Add(insertNode);
            }
        }

        InsertNodesToFormulaList(formulaList, i, needInsertNodes);
    }

    private void InsertNodesToFormulaList(List<FormulaNode> formulaList, int idx, List<FormulaNode> needInsertNodes)
    {
        int curIndex = idx;
        formulaList.RemoveAt(curIndex);
        for(int i = 0; i < needInsertNodes.Count; ++i)
        {
            formulaList.Insert(curIndex, needInsertNodes[i]);
            ++curIndex;
        }
    }

    private void DoReplaceFormula(List<FormulaNode> initFormulaList, List<FormulaNode> formulaList, RulePair rp)
    {
        int i = 0;
        while (i < formulaList.Count)
        {
            if (formulaList[i].Type == FormulaType.FT_FUN)
            {
                DoReplaceFun(rp, formulaList, i);
            }
            else if(formulaList[i].Type == FormulaType.FT_STR)
            {
                if (RuleKeyIsExp(rp) == false)
                {
                    DoReplaceStr(rp, formulaList, i);
                }
            }

            ++i;
        }
    }

    private bool RuleKeyIsExp(RulePair rp)
    {
        bool isExp = false;
        if (rp.Key.Contains(":") == true
            || rp.Key.Contains("*") == true
            || rp.Key.Contains(">") == true
            || rp.Key.Contains("<") == true
            || rp.Key.Contains(">=") == true
            || rp.Key.Contains("<=") == true)
        {
            isExp = true;
        }

        return isExp;
    }

    private string GetPrevNode(List<FormulaNode> formulaList, int idx)
    {
        int i = idx;
        int deepCount = 0;
        string prevNode = "";
        while(i > 0)
        {
            FormulaNode node = formulaList[i - 1];
            if(node.Replaced == true)
            {
                break;
            }

            bool skip = false;

            if(node.Node == "]")
            {
                ++deepCount;
                skip = true;
            }
            else if(node.Node == "[")
            {
                --deepCount;
                if(deepCount < 0)
                {
                    break;
                }

                skip = true;
            }

            if(mIgnoreList.Contains(node.Node) == true)
            {
                skip = true;
            }

            if(skip == true)
            {
                --i;
                continue;
            }

            if(deepCount == 0)
            {
                prevNode = node.Node;
                break;
            }

            --i;
        }

        return prevNode;
    }

    private string GetNextNode(List<FormulaNode> formulaList, int idx)
    {
        int i = idx;
        int deepCount = 0;
        string nextNode = "";
        while (i < formulaList.Count - 1)
        {
            FormulaNode node = formulaList[i + 1];
            if (node.Replaced == true)
            {
                break;
            }

            bool skip = false;

            if (node.Node == "[")
            {
                ++deepCount;
                skip = true;
            }
            else if (node.Node == "]")
            {
                --deepCount;
                if(deepCount < 0)
                {
                    break;
                }

                skip = true;
            }

            if(skip == true)
            {
                ++i;
                continue;
            }

            if (deepCount == 0)
            {
                nextNode = node.Node;
                break;
            }

            ++i;
        }

        return nextNode;
    }

    private void ResetFormulaListReplaceState(List<FormulaNode> formulaList)
    {
        for(int i = 0; i < formulaList.Count; ++i)
        {
            formulaList[i].Replaced = false;
        }
    }

    private void ReplaceFormulaOnePass()
    {
        List<FormulaNode> initFormulist = CreateFormulaNodeList(mFormula);
        List<FormulaNode> formulaList = new List<FormulaNode>();
        for (int i = 0; i < initFormulist.Count; ++i)
        {
            FormulaNode node = new FormulaNode();
            node.Node = initFormulist[i].Node;
            node.Type = initFormulist[i].Type;
            formulaList.Add(node);
        }

        for (int i = 0; i < mRule.Count; ++i)
        {
            RulePair rp = mRule[i];
            DoReplaceFormula(initFormulist, formulaList, rp);
            ResetFormulaListReplaceState(initFormulist);
        }

        mFormula = "";

        foreach (FormulaNode item in formulaList)
        {
            if (mFormula == "")
            {
                mFormula = item.Node;
            }
            else
            {
                mFormula += " ";
                mFormula += item.Node;
            }
        }
    }

    private void ReplaceFormulaOnce(RulePair rp)
    {
        List<FormulaNode> initFormulist = CreateFormulaNodeList(mFormula);
        List<FormulaNode> formulaList = new List<FormulaNode>();
        for (int i = 0; i < initFormulist.Count; ++i)
        {
            FormulaNode node = new FormulaNode();
            node.Node = initFormulist[i].Node;
            formulaList.Add(node);
        }

        DoReplaceFormula(initFormulist, formulaList, rp);

        mFormula = "";

        foreach (FormulaNode item in formulaList)
        {
            if (mFormula == "")
            {
                mFormula = item.Node;
            }
            else
            {
                mFormula += " ";
                mFormula += item.Node;
            }
        }
    }

    private void UpdateFormulaWithRand()
    {
        int ruleIndex = Util.GetRouletteIndex(mProb);
        RulePair rp = mRule[ruleIndex];

        ReplaceFormulaOnce(rp);
    }

    private void UpdateFormulaWithSeq()
    {
        ReplaceFormulaOnePass();
    }

    private bool ExistInList(string s, List<string> lst)
    {
        bool ret = false;
        for(int i = 0; i < lst.Count; ++i)
        {
            if(lst[i] == s)
            {
                ret = true;
                break;
            }
        }

        return ret;
    }

    //private float CalcPoExp(FormulaFunc formulaFunc, FormulaFunc ruleFunc, string exp)
    private FormulaFunc CalcPoExp(FormulaFunc formulaFunc, FormulaFunc ruleFunc, FormulaFunc expFunc)
    {
        FormulaFunc resultFunc = new FormulaFunc();
        resultFunc.FuncName = expFunc.FuncName;
        for (int k = 0; k < expFunc.args.Count; ++k)
        {
            string[] expArr = expFunc.args[k].Split(' ');
            for (int i = 0; i < expArr.Length; ++i)
            {
                if (ExistInList(expArr[i], ruleFunc.args) == true)
                {
                    // 用值替换字符
                    expArr[i] = formulaFunc.args[i];
                }
            }

            Stack<float> expStack = new Stack<float>();
            // 求值
            for (int i = 0; i < expArr.Length; ++i)
            {
                if (expStack.Count >= 2 && (expArr[i] == "+" || expArr[i] == "-" || expArr[i] == "*" || expArr[i] == "/"))
                {
                    float right = expStack.Pop();
                    float left = expStack.Pop();

                    if (expArr[i] == "+")
                    {
                        expStack.Push(left + right);
                    }
                    else if (expArr[i] == "-")
                    {
                        expStack.Push(left - right);
                    }
                    else if (expArr[i] == "*")
                    {
                        expStack.Push(left * right);
                    }
                    else if (expArr[i] == "/")
                    {
                        expStack.Push(left / right);
                    }
                }
                else
                {
                    expStack.Push(float.Parse(expArr[i]));
                }
            }

            if (expStack.Count != 1)
            {
                Util.ERR("err in calc poexp!");
            }

            resultFunc.args.Add(expStack.Pop().ToString());
        }
        return resultFunc;
    }

    // TEST:"A.+.B.*.(.C.-.D.)./.E.+.F./.H"
    private string ToPoExp(string exp)
    {
        string poExp = "";
        List<string> poExpList = new List<string>();

        string[] expArr = exp.Split(' ');
        Stack<string> stack = new Stack<string>();

        for(int i = 0; i < expArr.Length; ++i)
        {
            string c = expArr[i];
            if (c == "+" || c == "-")
            {
                if (stack.Count == 0 || stack.Peek() == "(")
                {
                    stack.Push(c);
                }
                else
                {
                    while (stack.Count > 0
                        && (stack.Peek() == "*"
                        || stack.Peek() == "/"
                        || stack.Peek() == "+"
                        || stack.Peek() == "-"))
                    {
                        string s = stack.Pop();
                        poExpList.Add(s);
                        //Util.DBG(s);
                    }

                    stack.Push(c);
                }
            }
            else if (c == "*" || c == "/")
            {
                if (stack.Count == 0 || stack.Peek() == "+" || stack.Peek() == "-" || stack.Peek() == "(")
                {
                    stack.Push(c);
                }
                else
                {
                    while (stack.Count > 0
                        && (stack.Peek() == "/"
                        || stack.Peek() == "*"))
                    {
                        string s = stack.Pop();
                        poExpList.Add(s);
                        //Util.DBG(s);
                    }
                    stack.Push(c);
                }
            }
            else if(c == "(")
            {
                stack.Push(c);
            }
            else if(c == ")")
            {
                string t;
                while((t = stack.Pop()) != "(")
                {
                    poExpList.Add(t);
                    //Util.DBG(t);
                }
            }
            else
            {
                poExpList.Add(c);
                //Util.DBG(c);
            }
        }

        if(stack.Count > 0)
        {
            while(stack.Count > 0)
            {
                string s = stack.Pop();
                poExpList.Add(s);
                //Util.DBG(s);
            }
        }

        for(int i = 0; i < poExpList.Count; ++i)
        {
            if(poExp == "")
            {
                poExp = poExpList[i];
            }
            else
            {
                poExp += " ";
                poExp += poExpList[i];
            }
        }

        //Util.DBG("PoExp = " + poExp);

        return poExp;
    }

    private Dictionary<string, object> mPlantCfg;
    private int mRepeatCount;
    private string mType;
    private List<RulePair> mRule;
    private string mFormula;
    private float mTurnAngle;
    private List<object> mProb;
    private List<string> mIgnoreList;
}