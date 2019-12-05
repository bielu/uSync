﻿using System;

using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;

using uSync8.BackOffice;
using uSync8.BackOffice.Configuration;
using uSync8.BackOffice.Services;
using uSync8.BackOffice.SyncHandlers;
using uSync8.Core.Dependency;
using uSync8.Core.Serialization;
using uSync8.Core.Tracking;

using static Umbraco.Core.Constants;

namespace uSync8.ContentEdition.Handlers
{
    [SyncHandler("mediaHandler", "Media", "Media", uSyncBackOfficeConstants.Priorites.Media,
        Icon = "icon-picture usync-addon-icon", IsTwoPass = true, EntityType = UdiEntityType.Media)]
    public class MediaHandler : SyncHandlerTreeBase<IMedia, IMediaService>, ISyncHandler, ISyncExtendedHandler
    {
        public override string Group => uSyncBackOfficeConstants.Groups.Content;

        private readonly IMediaService mediaService;

        public MediaHandler(
            IEntityService entityService,
            IProfilingLogger logger,
            IMediaService mediaService,
            ISyncSerializer<IMedia> serializer,
            ISyncTracker<IMedia> tracker,
            ISyncDependencyChecker<IMedia> checker,
            SyncFileService syncFileService)
            : base(entityService, logger, serializer, tracker, checker, syncFileService)
        {
            this.mediaService = mediaService;
        }

        protected override void DeleteViaService(IMedia item)
            => mediaService.Delete(item);

        protected override IMedia GetFromService(int id)
            => mediaService.GetById(id);

        protected override IMedia GetFromService(Guid key)
        {
            // FIX: alpha bug - getby key is not always uptodate 
            var entity = entityService.Get(key);
            if (entity != null)
                return mediaService.GetById(entity.Id);

            return null;
        }


        protected override IMedia GetFromService(string alias)
            => null;

        protected override void InitializeEvents(HandlerSettings settings)
        {
            MediaService.Saved += EventSavedItem;
            MediaService.Deleted += EventDeletedItem;
            MediaService.Moved += EventMovedItem;
            MediaService.Trashed += EventMovedItem;
        }
    }
}
