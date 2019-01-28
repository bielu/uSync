﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Entities;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using uSync8.BackOffice.Services;
using uSync8.Core;
using uSync8.Core.Serialization;
using uSync8.Core.Tracking;

namespace uSync8.BackOffice.SyncHandlers.Handlers
{
    [SyncHandler("dataTypeHandler", "Datatypes", "DataTypes", uSyncBackOfficeConstants.Priorites.DataTypes, Icon = "icon-autofill")]
    public class DataTypeHandler : SyncHandlerTreeBase<IDataType, IDataTypeService>, ISyncHandler, ISyncPostImportHandler
    {
        private readonly IDataTypeService dataTypeService;

        public DataTypeHandler(
            IEntityService entityService,
            IDataTypeService dataTypeService,
            IProfilingLogger logger, 
            ISyncSerializer<IDataType> serializer, 
            ISyncTracker<IDataType> tracker,
            SyncFileService syncFileService, 
            uSyncBackOfficeSettings settings) 
            : base(entityService, logger, serializer, tracker, syncFileService, settings)
        {
            this.dataTypeService = dataTypeService;
            this.itemObjectType = UmbracoObjectTypes.DataType;
        }

        protected override IDataType GetFromService(int id)
            => dataTypeService.GetDataType(id);

        protected override void InitializeEvents()
        {
            DataTypeService.Saved += EventSavedItem;
            DataTypeService.Deleted += EventDeletedItem;
        }

        protected override string GetItemFileName(IUmbracoEntity item)
            => item.Name.ToSafeFileName();

        protected override void DeleteFolder(int id)
            => dataTypeService.DeleteContainer(id);

        public override IEnumerable<uSyncAction> ProcessPostImport(string folder, IEnumerable<uSyncAction> actions, uSyncHandlerSettings config)
        {
            if (actions == null || !actions.Any())
                return null;

            foreach (var action in actions)
            {
                var attempt = Import(action.FileName, config);
                if (attempt.Success)
                {
                    ImportSecondPass(action.FileName, attempt.Item, config);
                }
            }

            return CleanFolders(folder, -1);
        }

        protected override IDataType GetFromService(Guid key)
            => dataTypeService.GetDataType(key);

        protected override IDataType GetFromService(string alias)
            => dataTypeService.GetDataType(alias);

        protected override void DeleteViaService(IDataType item)
            => dataTypeService.Delete(item);
    }
}