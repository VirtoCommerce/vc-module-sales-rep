// Call this to register your module to main application
var moduleName = 'VirtoCommerce.SalesRep';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.SalesRepState', {
                    url: '/sales-rep',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'VirtoCommerce.SalesRep.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.SalesRep)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/sales-rep',
                icon: 'fa fa-cube',
                title: 'Sales Rep',
                priority: 100,
                action: function () { $state.go('workspace.SalesRepState'); },
                permission: 'sales-rep:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
