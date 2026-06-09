using EEGTool.Models.Template;
using FrameWork.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.Models.Collection
{

    //采集信息
    public class CollectionInfo
    {
        public int SampleRate { get; set; } = 0;
        public TemplateModel Template { get; set; } = new TemplateModel();
        public bool IsCaptureVideo { get; set; } = false;
        public bool ConfigureCommandSent { get; set; } = false;
    }


    public class CollectionInfoManager : Singleton<CollectionInfoManager>
    {
        private CollectionInfo _info = new CollectionInfo();

        public CollectionInfo Info
        {
            get
            {
                return _info;
            }
        }

        public CollectionInfoManager()
        {

        }

        public void UpdateInfo(CollectionInfo info)
        {
            _info = info;
        }
    }
}
