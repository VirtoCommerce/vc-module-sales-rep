// This file is auto-generated. Do not edit manually.

import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';
export type Maybe<T> = T;
export type InputMaybe<T> = T;
export type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
export type MakeOptional<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]?: Maybe<T[SubKey]> };
export type MakeMaybe<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]: Maybe<T[SubKey]> };
export type MakeEmpty<T extends { [key: string]: unknown }, K extends keyof T> = { [_ in K]?: never };
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
/** All built-in and custom scalars, mapped to their actual values */
export type Scalars = {
  ID: { input: string; output: string; }
  String: { input: string; output: string; }
  Boolean: { input: boolean; output: boolean; }
  Int: { input: number; output: number; }
  Float: { input: number; output: number; }
  AnyValue: { input: any; output: any; }
  DateTime: { input: any; output: any; }
};

export type InputLayout = {
  /** Fixed regions of the dashboard with their blocks; replaces the saved layout as a whole. */
  regions: Array<InputLayoutRegion>;
  /** Document schema version. */
  schemaVersion: Scalars['Int']['input'];
  /** Dashboard the layout belongs to ("accountDashboard"). */
  scope: Scalars['String']['input'];
  /** Store the layout is saved for. */
  storeId?: InputMaybe<Scalars['String']['input']>;
};

export type InputLayoutBlock = {
  /** Whether the user hid the block. */
  hidden: Scalars['Boolean']['input'];
  /** Block id, unique within the layout. */
  id: Scalars['String']['input'];
  /** Block settings; send an empty list for none. */
  settings: Array<InputLayoutSetting>;
  /** Block type, from the storefront vocabulary. */
  type: Scalars['String']['input'];
};

export type InputLayoutRegion = {
  /** Blocks in render order. */
  blocks: Array<InputLayoutBlock>;
  /** Region id ("statistics", "mainLeft"). */
  id: Scalars['String']['input'];
};

export type InputLayoutSetting = {
  /** Setting key, from the block type vocabulary. */
  key: Scalars['String']['input'];
  /** Scalar setting value: string, number or boolean. */
  value?: InputMaybe<Scalars['AnyValue']['input']>;
};

export type Layout = {
  /** When the layout was last saved (UTC). */
  modifiedDate?: Maybe<Scalars['DateTime']['output']>;
  /** Fixed regions of the dashboard. */
  regions: Array<LayoutRegion>;
  /** Document schema version, for the storefront to migrate older saved layouts. */
  schemaVersion: Scalars['Int']['output'];
};

export type LayoutBlock = {
  /** Whether the user hid the block. */
  hidden: Scalars['Boolean']['output'];
  /** Block id, unique within the layout. */
  id: Scalars['String']['output'];
  /** Block settings; may be empty. */
  settings: Array<LayoutSetting>;
  /** Block type, from the storefront vocabulary. */
  type: Scalars['String']['output'];
};

export type LayoutRegion = {
  /** Blocks in render order. */
  blocks: Array<LayoutBlock>;
  /** Region id ("statistics", "mainLeft"). */
  id: Scalars['String']['output'];
};

export type LayoutSetting = {
  /** Setting key, from the block type vocabulary. */
  key: Scalars['String']['output'];
  /** Scalar setting value: string, number or boolean. */
  value?: Maybe<Scalars['AnyValue']['output']>;
};

export type Mutations = {
  saveLayout?: Maybe<Layout>;
};


export type MutationsSaveLayoutArgs = {
  command: InputLayout;
};

export type Query = {
  layout?: Maybe<Layout>;
};


export type QueryLayoutArgs = {
  scope: Scalars['String']['input'];
  storeId?: InputMaybe<Scalars['String']['input']>;
};

export type SaveLayoutMutationVariables = Exact<{
  command: InputLayout;
}>;


export type SaveLayoutMutation = { saveLayout?: { regions: Array<{ id: string, blocks: Array<{ id: string, type: string, hidden: boolean, settings: Array<{ key: string, value?: any }> }> }> } };

export type LayoutQueryVariables = Exact<{
  scope: Scalars['String']['input'];
  storeId?: InputMaybe<Scalars['String']['input']>;
}>;


export type LayoutQuery = { layout?: { regions: Array<{ id: string, blocks: Array<{ id: string, type: string, hidden: boolean, settings: Array<{ key: string, value?: any }> }> }> } };


export const SaveLayoutDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"SaveLayout"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"command"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"InputLayout"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"saveLayout"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"command"},"value":{"kind":"Variable","name":{"kind":"Name","value":"command"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"regions"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"blocks"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"hidden"}},{"kind":"Field","name":{"kind":"Name","value":"settings"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"key"}},{"kind":"Field","name":{"kind":"Name","value":"value"}}]}}]}}]}}]}}]}}]} as unknown as DocumentNode<SaveLayoutMutation, SaveLayoutMutationVariables>;
export const LayoutDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"Layout"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"scope"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"storeId"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"layout"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"scope"},"value":{"kind":"Variable","name":{"kind":"Name","value":"scope"}}},{"kind":"Argument","name":{"kind":"Name","value":"storeId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"storeId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"regions"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"blocks"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"hidden"}},{"kind":"Field","name":{"kind":"Name","value":"settings"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"key"}},{"kind":"Field","name":{"kind":"Name","value":"value"}}]}}]}}]}}]}}]}}]} as unknown as DocumentNode<LayoutQuery, LayoutQueryVariables>;
export const OperationNames = {
  Query: {
    Layout: 'Layout'
  },
  Mutation: {
    SaveLayout: 'SaveLayout'
  }
}