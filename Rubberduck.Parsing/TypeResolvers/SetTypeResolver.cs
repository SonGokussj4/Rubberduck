﻿using System;
using System.Collections.Generic;
using System.Linq;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Parsing.VBA.DeclarationCaching;
using Rubberduck.VBEditor;

namespace Rubberduck.Parsing.TypeResolvers
{
    public class SetTypeResolver :  ISetTypeResolver
    {
        public const string NotAnObject = "NotAnObject";


        private readonly IDeclarationFinderProvider _declarationFinderProvider;

        public SetTypeResolver(IDeclarationFinderProvider declarationFinderProvider)
        {
            _declarationFinderProvider = declarationFinderProvider;
        }

        public Declaration SetTypeDeclaration(VBAParser.ExpressionContext expression, QualifiedModuleName containingModule)
        {
            switch (expression)
            {
                case VBAParser.LExprContext lExpression:
                    return SetTypeDeclaration(lExpression.lExpression(), containingModule);
                case VBAParser.NewExprContext newExpression:
                    return SetTypeDeclaration(newExpression.expression(), containingModule);
                case VBAParser.TypeofexprContext typeOfExpression:
                    return SetTypeDeclaration(typeOfExpression.expression(), containingModule);
                default:
                    return null; //All remaining expression types either have no Set type or there is no declaration for it. 
            }
        }

        private Declaration SetTypeDeclaration(VBAParser.LExpressionContext lExpression, QualifiedModuleName containingModule)
        {
            var finder = _declarationFinderProvider.DeclarationFinder;
            var setTypeDeterminingDeclaration =
                SetTypeDeterminingDeclarationOfExpression(lExpression, containingModule, finder);
            return SetTypeDeclaration(setTypeDeterminingDeclaration.declaration);
        }

        private Declaration SetTypeDeclaration(Declaration setTypeDeterminingDeclaration)
        {
            return setTypeDeterminingDeclaration?.DeclarationType.HasFlag(DeclarationType.ClassModule) ?? true
                ? setTypeDeterminingDeclaration
                : setTypeDeterminingDeclaration.AsTypeDeclaration;
        }


        public string SetTypeName(VBAParser.ExpressionContext expression, QualifiedModuleName containingModule)
        {
            switch (expression)
            {
                case VBAParser.LExprContext lExpression:
                    return SetTypeName(lExpression.lExpression(), containingModule);
                case VBAParser.NewExprContext newExpression:
                    return SetTypeName(newExpression.expression(), containingModule);
                case VBAParser.TypeofexprContext typeOfExpression:
                    return SetTypeName(typeOfExpression.expression(), containingModule);
                case VBAParser.LiteralExprContext literalExpression:
                    return SetTypeName(literalExpression.literalExpression());
                case VBAParser.BuiltInTypeExprContext builtInTypeExpression:
                    return SetTypeName(builtInTypeExpression.builtInType());
                default:
                    return NotAnObject; //All remaining expression types have no Set type. 
            }
        }

        private string SetTypeName(VBAParser.LiteralExpressionContext literalExpression)
        {
            var literalIdentifier = literalExpression.literalIdentifier();

            if (literalIdentifier?.objectLiteralIdentifier() != null)
            {
                return Tokens.Object;
            }

            return NotAnObject;
        }

        private string SetTypeName(VBAParser.BuiltInTypeContext builtInType)
        {
            if (builtInType.OBJECT() != null)
            {
                return Tokens.Object;
            }

            var baseType = builtInType.baseType();

            if (baseType.VARIANT() != null)
            {
                return Tokens.Variant;
            }

            if (baseType.ANY() != null)
            {
                return Tokens.Any;
            }

            return NotAnObject;
        }

        private string SetTypeName(VBAParser.LExpressionContext lExpression, QualifiedModuleName containingModule)
        {
            var finder = _declarationFinderProvider.DeclarationFinder;
            var setTypeDeterminingDeclaration = SetTypeDeterminingDeclarationOfExpression(lExpression, containingModule, finder);
            return setTypeDeterminingDeclaration.mightHaveSetType
                ? FullObjectTypeName(setTypeDeterminingDeclaration.declaration, lExpression)
                : NotAnObject;
        }

        private static string FullObjectTypeName(Declaration setTypeDeterminingDeclaration, VBAParser.LExpressionContext lExpression)
        {
            if (setTypeDeterminingDeclaration == null)
            {
                //This is a workaround because built-in type expressions tent to get matched by simple name expressions instead.
                return BuiltInObjectTypeNameFromUnresolvedlExpression(lExpression);
            }

            if (setTypeDeterminingDeclaration.DeclarationType.HasFlag(DeclarationType.ClassModule))
            {
                return setTypeDeterminingDeclaration.QualifiedModuleName.ToString();
            }

            if (setTypeDeterminingDeclaration.IsObject)
            {
                return setTypeDeterminingDeclaration.FullAsTypeName;
            }

            return setTypeDeterminingDeclaration.AsTypeName == Tokens.Variant
                ? setTypeDeterminingDeclaration.AsTypeName
                : NotAnObject;
        }

        private static string BuiltInObjectTypeNameFromUnresolvedlExpression(VBAParser.LExpressionContext lExpression)
        {
            var expressionText = WithBracketsRemoved(lExpression.GetText());

            if (_builtInObjectTypeNames.Contains(expressionText))
            {
                return expressionText;
            }

            if (_builtInNonObjectTypeNames.Contains(expressionText))
            {
                return NotAnObject;
            }

            return null;
        }

        private static string WithBracketsRemoved(string input)
        {
            if (input.StartsWith("[") && input.EndsWith("]"))
            {
                return input.Substring(1, input.Length - 2);
            }

            return input;
        }

        private static List<string> _builtInObjectTypeNames = new List<string>
        {
            Tokens.Any,
            Tokens.Variant,
            Tokens.Object
        };

        private static List<string> _builtInNonObjectTypeNames = new List<string>
        {
            Tokens.Boolean,
            Tokens.Byte,
            Tokens.Currency,
            Tokens.Date,
            Tokens.Double,
            Tokens.Integer,
            Tokens.Long,
            Tokens.LongLong,
            Tokens.LongPtr,
            Tokens.Single,
            Tokens.String
        };


        private (Declaration declaration, bool mightHaveSetType) SetTypeDeterminingDeclarationOfExpression(VBAParser.LExpressionContext lExpression, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            switch (lExpression)
            {
                case VBAParser.SimpleNameExprContext simpleNameExpression:
                    return SetTypeDeterminingDeclarationOfExpression(simpleNameExpression.identifier(), containingModule, finder);
                case VBAParser.InstanceExprContext instanceExpression:
                    return SetTypeDeterminingDeclarationOfInstance(containingModule, finder);
                case VBAParser.IndexExprContext indexExpression:
                    throw new NotImplementedException();
                case VBAParser.MemberAccessExprContext memberAccessExpression:
                    return SetTypeDeterminingDeclarationOfExpression(memberAccessExpression.unrestrictedIdentifier(), containingModule, finder);
                case VBAParser.WithMemberAccessExprContext withMemberAccessExpression:
                    return SetTypeDeterminingDeclarationOfExpression(withMemberAccessExpression.unrestrictedIdentifier(), containingModule, finder);
                case VBAParser.DictionaryAccessExprContext dictionaryAccessExpression:
                    throw new NotImplementedException();
                case VBAParser.WithDictionaryAccessExprContext withDictionaryAccessExpression:
                    throw new NotImplementedException();
                case VBAParser.WhitespaceIndexExprContext whitespaceIndexExpression:
                    throw new NotImplementedException();
                default:
                    return (null, true);    //We should already cover every case. Return the value indicating that we have no idea.
            }
        }

        private (Declaration declaration, bool mightHaveSetType) SetTypeDeterminingDeclarationOfExpression(VBAParser.IdentifierContext identifier, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            var declaration = finder.IdentifierReferences(identifier, containingModule)
                .Select(reference => reference.Declaration)
                .FirstOrDefault();
            return (declaration, MightHaveSetType(declaration));
        }

        private (Declaration declaration, bool mightHaveSetType) SetTypeDeterminingDeclarationOfExpression(VBAParser.UnrestrictedIdentifierContext identifier, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            var declaration = finder.IdentifierReferences(identifier, containingModule)
                .Select(reference => reference.Declaration)
                .FirstOrDefault();
            return (declaration, MightHaveSetType(declaration));
        }

        private static bool MightHaveSetType(Declaration declaration)
        {
            return declaration == null
                   || declaration.IsObject
                   || declaration.AsTypeName == Tokens.Variant
                   || declaration.DeclarationType.HasFlag(DeclarationType.ClassModule);
        }

        private (Declaration declaration, bool mightHaveSetType) SetTypeDeterminingDeclarationOfInstance(QualifiedModuleName instance, DeclarationFinder finder)
        {
            var classDeclaration = finder.Classes.FirstOrDefault(cls => cls.QualifiedModuleName.Equals(instance));
            return (classDeclaration, true);
        }
    }
}