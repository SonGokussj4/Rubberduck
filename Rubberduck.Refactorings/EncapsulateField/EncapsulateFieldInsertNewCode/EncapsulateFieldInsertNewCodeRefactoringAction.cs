﻿using Rubberduck.Parsing.Rewriter;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Refactorings.Common;
using Rubberduck.Resources;
using System;
using System.Diagnostics;
using System.Linq;
using Rubberduck.Refactorings.EncapsulateField;
using System.Collections.Generic;

namespace Rubberduck.Refactorings.EncapsulateFieldInsertNewCode
{
    public class EncapsulateFieldInsertNewCodeRefactoringAction : CodeOnlyRefactoringActionBase<EncapsulateFieldInsertNewCodeModel>
    {
        private readonly IDeclarationFinderProvider _declarationFinderProvider;
        private readonly IPropertyAttributeSetsGenerator _propertyAttributeSetsGenerator;
        private readonly IEncapsulateFieldCodeBuilder _encapsulateFieldCodeBuilder;

        public EncapsulateFieldInsertNewCodeRefactoringAction(
            IDeclarationFinderProvider declarationFinderProvider, 
            IRewritingManager rewritingManager,
            IPropertyAttributeSetsGenerator propertyAttributeSetsGenerator,
            IEncapsulateFieldCodeBuilderFactory encapsulateFieldCodeBuilderFactory)
                : base(rewritingManager)
        {
            _declarationFinderProvider = declarationFinderProvider;
            _propertyAttributeSetsGenerator = propertyAttributeSetsGenerator;
            _encapsulateFieldCodeBuilder = encapsulateFieldCodeBuilderFactory.Create();
        }

        public override void Refactor(EncapsulateFieldInsertNewCodeModel model, IRewriteSession rewriteSession)
        {
            if (model.CreateNewObjectStateUDT)
            {
                var objectStateFieldDeclaration = _encapsulateFieldCodeBuilder.BuildObjectStateFieldDeclaration(model.ObjectStateUDTField);
                model.NewContentAggregator.AddNewContent(NewContentType.DeclarationBlock, objectStateFieldDeclaration);

                var objectStateTypeDeclarationBlock = _encapsulateFieldCodeBuilder.BuildUserDefinedTypeDeclaration(model.ObjectStateUDTField, model.SelectedFieldCandidates);
                model.NewContentAggregator.AddNewContent(NewContentType.UserDefinedTypeDeclaration, objectStateTypeDeclarationBlock);
            }

            LoadNewPropertyBlocks(model, rewriteSession);

            InsertBlocks(model, rewriteSession);

            model.NewContentAggregator = null;
        }

        private void LoadNewPropertyBlocks(EncapsulateFieldInsertNewCodeModel model, IRewriteSession rewriteSession)
        {
            var propAttributeSets = model.SelectedFieldCandidates
                .SelectMany(f => _propertyAttributeSetsGenerator.GeneratePropertyAttributeSets(f)).ToList();

            foreach (var propertyAttributeSet in propAttributeSets)
            {
                Debug.Assert(propertyAttributeSet.Declaration.DeclarationType.HasFlag(DeclarationType.Variable) || propertyAttributeSet.Declaration.DeclarationType.HasFlag(DeclarationType.UserDefinedTypeMember));

                var (Get, Let, Set) = _encapsulateFieldCodeBuilder.BuildPropertyBlocks(propertyAttributeSet);

                var blocks = new List<string>() { Get, Let, Set };
                blocks.ForEach(s => model.NewContentAggregator.AddNewContent(NewContentType.CodeSectionBlock, s));
            }
        }

        private void InsertBlocks(EncapsulateFieldInsertNewCodeModel model, IRewriteSession rewriteSession)
        {

            var newDeclarationSectionBlock = model.NewContentAggregator.RetrieveBlock(NewContentType.UserDefinedTypeDeclaration, NewContentType.DeclarationBlock, NewContentType.CodeSectionBlock);
            if (string.IsNullOrEmpty(newDeclarationSectionBlock))
            {
                return;
            }

            var doubleSpace = $"{Environment.NewLine}{Environment.NewLine}";

            var allNewContent = string.Join(doubleSpace, new string[] { newDeclarationSectionBlock });

            var previewMarker = model.NewContentAggregator.RetrieveBlock(RubberduckUI.EncapsulateField_PreviewMarker);
            if (!string.IsNullOrEmpty(previewMarker))
            {
                allNewContent = $"{allNewContent}{Environment.NewLine}{previewMarker}";
            }

            var rewriter = rewriteSession.CheckOutModuleRewriter(model.QualifiedModuleName);

            var codeSectionStartIndex = _declarationFinderProvider.DeclarationFinder
                .Members(model.QualifiedModuleName).Where(m => m.IsMember())
                .OrderBy(c => c.Selection)
                .FirstOrDefault()?.Context.Start.TokenIndex;

            if (codeSectionStartIndex.HasValue)
            {
                rewriter.InsertBefore(codeSectionStartIndex.Value, $"{allNewContent}{doubleSpace}");
                return;
            }
            rewriter.InsertBefore(rewriter.TokenStream.Size - 1, $"{doubleSpace}{allNewContent}");
        }
    }
}
