using System.Collections.Generic;
using Rubberduck.VBEditor.Events;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace Rubberduck.UI.CodeExplorer.Commands
{
    public class AddUserControlCommand : AddComponentCommandBase
    {
        public AddUserControlCommand(IVBE vbe, IVBEEvents vbeEvents) : base(vbe, vbeEvents) { }

        public override IEnumerable<ProjectType> AllowableProjectTypes => ProjectTypes.VB6;

        public override ComponentType ComponentType => ComponentType.UserControl;
    }
}
