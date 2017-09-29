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
    FT_CONTEXT, // 有上下文
}

public class FormulaFunc
{
    public FormulaFunc()
    {
        FuncName = "";
        Type = FormulaType.FT_NUM;
        args = new List<string>();
        PrevFunc = null;
        NextFunc = null;
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
    public FormulaFunc PrevFunc;
    public FormulaFunc NextFunc;
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

        mDefine = new Dictionary<string, string>();
        if (mPlantCfg.ContainsKey("define") == true)
        {
            Dictionary<string, object> defineList = mPlantCfg["define"] as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> kv in defineList)
            {
                // TODO: 如果有表达式，则需要计算
                mDefine.Add(kv.Key, kv.Value.ToString());
            }
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
                string key = item[0].ToString();
                string value = item[1].ToString();

                key = ReplaceDefineStrInFormula(key);
                value = ReplaceDefineStrInFormula(value);
                RulePair rp = new RulePair(key, value);
                mRule.Add(rp);
            }
        }

        mFormula = ReplaceDefineStrInFormula(mFormula);

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

    private string ReplaceDefineStrInFormula(string str)
    {
        foreach(KeyValuePair<string, string> kv in mDefine)
        {
            str = str.Replace(kv.Key, kv.Value);
        }

        return str;
    }

    private bool CalcCondition(FormulaFunc formulaFunc, List<FormulaNode> formulaList, int idx, FormulaFunc ruleFunc, string condStr)
    {
        string[] condArr = condStr.Split(' ');

        bool result = true;
        CondOp curOP = CondOp.AND;

        // 有上下文则先判断上下文
        if(ruleFunc.PrevFunc != null)
        {
            if(idx - 1 >= 0)
            {
                FormulaNode prevNode = formulaList[idx - 1];
                FormulaFunc prevFormulaFunc = MakeFunByStr(prevNode.Node);
                if(prevFormulaFunc.FuncName != ruleFunc.PrevFunc.FuncName || prevFormulaFunc.args.Count != ruleFunc.PrevFunc.args.Count)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if(ruleFunc.NextFunc != null)
        {
            if(idx + 1 < formulaList.Count)
            {
                FormulaNode nextNode = formulaList[idx + 1];
                FormulaFunc nextFormulaFunc = MakeFunByStr(nextNode.Node);
                if(nextFormulaFunc.FuncName != ruleFunc.NextFunc.FuncName || nextFormulaFunc.args.Count != ruleFunc.NextFunc.args.Count)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

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

        bool hasPrev = false;
        bool hasNext = false;
        if(funStr.Contains("<") == true)
        {
            // 有前驱
            string[] prev_splited = funStr.Split('<');
            string prev = prev_splited[0];
            prev = prev.Trim();
            funType.PrevFunc = MakeFunByStr(prev);
            hasPrev = true;
        }

        if(funStr.Contains(">") == true)
        {
            string[] next_splited = funStr.Split('>');
            string next = next_splited[1];
            next = next.Trim();
            funType.NextFunc = MakeFunByStr(next);
            hasNext = true;
        }

        char[] operators = { '<', '>' };

        if (hasPrev == true && hasNext == true)
        {
            string[] splited = funStr.Split(operators);
            funStr = splited[1].Trim();
        }
        else if(hasPrev == true)
        {
            string[] splited = funStr.Split(operators[0]);
            funStr = splited[1].Trim();
        }
        else if(hasNext == true)
        {
            string[] splited = funStr.Split(operators[1]);
            funStr = splited[0].Trim();
        }

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
            string exp = BuildNoSpaceStr(ss);
            funType.args.Add(Util.ToPoExp(exp));
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

    private string BuildNoSpaceStr(string[] strArr)
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

    private bool DoReplaceFun(RulePair rp, List<FormulaNode> formulaList, int idx)
    {
        if(formulaList[idx].Replaced == true)
        {
            return false;
        }

        if (rp.Key.Contains(" ") == true)
        {
            string[] splitStr = rp.Key.Split(':');
            string ruleKeyFunStr = splitStr[0];
            string ruleKeyCondStr = splitStr[1];

            string[] ruleKeyFuncArr = ruleKeyFunStr.Split(' ');
            string ruleKeyFuncNoSpaceStr = BuildNoSpaceStr(ruleKeyFuncArr);

            FormulaFunc formulaFunc = MakeFunByStr(formulaList[idx].Node);

            // 解析函数
            FormulaFunc ruleFunc = MakeFunByStr(ruleKeyFuncNoSpaceStr);

            // 解析条件
            string[] ruleKeyCondArr = ruleKeyCondStr.Split(' ');
            string ruleKeyCondDotStr = BuildNoSpaceStr(ruleKeyCondArr);
            if (formulaFunc.FuncName == ruleFunc.FuncName && formulaFunc.args.Count == ruleFunc.args.Count && CalcCondition(formulaFunc, formulaList, idx, ruleFunc, ruleKeyCondDotStr) == true)
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

                return true;
            }
        }

        return false;
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
                bool result = DoReplaceFun(rp, formulaList, i);
                if(result == true)
                {
                    break;
                }
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

    private bool CalcLogicPoExp(FormulaFunc formulaFunc, FormulaFunc ruleFunc, string logicPoExp)
    {
        string[] expArr = logicPoExp.Split(' ');
        for(int i = 0; i < expArr.Length; ++i)
        {
            if(ExistInList(expArr[i], ruleFunc.args) == true)
            {
                expArr[i] = formulaFunc.args[i];
            }
        }

        Stack<string> expStack = new Stack<string>();

        // 求值
        for(int i = 0; i < expArr.Length; ++i)
        {
            if (expStack.Count >= 2 
                && (expArr[i] == ">" 
                || expArr[i] == ">=" 
                || expArr[i] == "==" 
                || expArr[i] == "!="
                || expArr[i] == "<"
                || expArr[i] == "<="
                || expArr[i] == "&&"
                || expArr[i] == "||"))
            {
                string rightStr = expStack.Pop();
                string leftStr = expStack.Pop();

                if (expArr[i] == ">")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left > right).ToString());
                }
                else if (expArr[i] == ">=")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left >= right).ToString());
                }
                else if (expArr[i] == "==")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left == right).ToString());
                }
                else if (expArr[i] == "!=")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left != right).ToString());
                }
                else if (expArr[i] == "<")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left < right).ToString());
                }
                else if (expArr[i] == "<=")
                {
                    float right = float.Parse(rightStr);
                    float left = float.Parse(leftStr);

                    expStack.Push((left <= right).ToString());
                }
                else if (expArr[i] == "&&")
                {
                    bool right = bool.Parse(rightStr);
                    bool left = bool.Parse(leftStr);

                    expStack.Push((left && right).ToString());
                }
                else if (expArr[i] == "||")
                {
                    bool right = bool.Parse(rightStr);
                    bool left = bool.Parse(leftStr);

                    expStack.Push((left || right).ToString());
                }
            }
            else
            {
                expStack.Push(expArr[i]);
            }
        }

        return bool.Parse(expStack.Pop());
    }

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

    private Dictionary<string, object> mPlantCfg;
    private int mRepeatCount;
    private string mType;
    private List<RulePair> mRule;
    private string mFormula;
    private float mTurnAngle;
    private List<object> mProb;
    private List<string> mIgnoreList;
    private Dictionary<string, string> mDefine;
}