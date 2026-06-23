angular.module('VirtoCommerce.SalesRep')
    .controller('VirtoCommerce.SalesRep.helloWorldController', ['$scope', 'VirtoCommerce.SalesRep.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'Sales Rep';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'sales-rep.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
