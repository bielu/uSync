(function () {
    'use strict';

    function uploadController($scope,
        notificationsService,
        navigationService, Upload,
        uSyncUploadService) {

        var vm = this;

        vm.buttonState = 'init';
        vm.uploaded = false;

        vm.close = close;
        vm.upload = uploadFiles;
        vm.applyChanges = applyChanges;

        vm.file = null;
        vm.fileChanges = fileChanges;
        vm.toggle = toggle;

        function close() {
            clean();

            if ($scope.model.close) {
                $scope.model.close();
            }
            else {
                navigationService.hideDialog();
            }
        }

        function fileChanges(files, event) {
            if (files && files.length > 0) {
                vm.file = files[0];
            }
        }

        function uploadFiles(file) {
            vm.buttonState = 'busy';

            vm.uploadedFile = file.name;

            Upload.upload({
                url: Umbraco.Sys.ServerVariables.uSync.uploadService + 'upload',
                fields: {},
                file: file
            }).success(function (data) {
                vm.buttonState = 'success';
                vm.result = data;
                vm.uploaded = true;
            }).error(function (error) {
                vm.buttonState = 'error';
                notificationsService.error('Error', 'Failed to upload file to server');
                console.log(error);
            });
        }

        function applyChanges() {

            vm.buttonState = 'busy';
            uSyncUploadService.applyChanges(vm.result)
                .then(function (data) {
                    vm.buttonState = 'success';
                    complete();
                    notificationsService.success('Reporting', 'Running report on newly imported files');
                }, function (error) {
                    vm.buttonState = 'error';
                    notificationsService.error('Error', 'Error importing files to site');
                    console.log(error);
                });
        }

        function complete() {
            clean();
            if ($scope.model.submit) {
                $scope.model.submit(vm.result);
            }
            else {
                navigationService.hideDialog();
            }
        }

        function toggle(folder) {
            console.log(folder);
        }

        function clean() {

            if (vm.result?.Id != undefined) {
                uSyncUploadService.clean(vm.result.Id)
                    .then(function () {
                        console.log('cleaned');
                    });
            }

        }

    }

    angular.module('umbraco')
        .controller('uSyncUploadController', uploadController);
})();