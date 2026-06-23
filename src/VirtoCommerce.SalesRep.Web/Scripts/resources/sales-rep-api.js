angular.module('VirtoCommerce.SalesRep')
    .factory('VirtoCommerce.SalesRep.webApi', ['$resource', function ($resource) {
        return $resource('api/sales-rep');
    }]);
