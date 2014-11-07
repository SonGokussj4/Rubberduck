﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetailCoderVBE.Reflection.VBA.Grammar
{
    internal class GlobalFieldSyntax : ISyntax
    {
        public SyntaxTreeNode ToNode(string instruction)
        {
            var pattern = VBAGrammar.GetModuleDeclarationSyntax(ReservedKeywords.Global);

            var match = Regex.Match(instruction, pattern);
            if (!match.Success)
            {
                return null;
            }

            return new VariableNode(match);
        }
    }
}
