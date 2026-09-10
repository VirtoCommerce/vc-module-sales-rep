// Managing Sales Reps is a customer-management action: the admin UI gates on the customer module's member
// permissions (the same ones the REST endpoints require), NOT on sales-rep:access (which only DEFINES a rep).
export default () => {
  // Member-management permissions (customer module — same as the contacts admin UI).
  const accessSalesRepPermission = "customer:read";
  const createSalesRepPermission = "customer:create";
  const readSalesRepPermission = "customer:read";
  const updateSalesRepPermission = "customer:update";
  const deleteSalesRepPermission = "customer:delete";
  // Account (login) permissions (platform security — like the customer "Accounts" widget). Create/update/delete
  // a rep also creates/edits/deletes a login account, so both the member AND the account permission are required.
  const accountCreatePermission = "platform:security:create";
  const accountManagementPermission = "platform:security:update";
  const accountDeletePermission = "platform:security:delete";
  // Shared documents library (VCST-5730): module-own permissions. Write implies read on the backend, so
  // read surfaces are gated on EITHER permission (hasAccess([...]) is any-of; Administrator always passes).
  const readDocumentsPermission = "sales-rep-documents:read";
  const writeDocumentsPermission = "sales-rep-documents:write";

  return {
    accessSalesRepPermission,
    createSalesRepPermission,
    readSalesRepPermission,
    updateSalesRepPermission,
    deleteSalesRepPermission,
    accountCreatePermission,
    accountManagementPermission,
    accountDeletePermission,
    readDocumentsPermission,
    writeDocumentsPermission,
  };
};
