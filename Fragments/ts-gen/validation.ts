// ts-gen/validation.ts
import { createRegistry, type Registry } from '@bufbuild/protobuf';
import type { GenFile } from '@bufbuild/protobuf/codegenv2';
import { createValidator, type Validator } from '@bufbuild/protovalidate';

// Import barrels that re-export your generated descriptors (GenFile)
import * as Auth from './Authentication/index.js';
import * as Authorization from './Authorization/index.js';
import * as Comment from './Comment/index.js';
import * as Content from './Content/index.js';
import * as CreatorDashboard from './CreatorDashboard/index.js';
import * as Generic from './Generic/index.js';
import * as Notification from './Notification/index.js';
import * as Page from './Page/index.js';
import * as Settings from './Settings/index.js';
import * as FragmentsRoot from './index.js';

// Runtime guard (don’t use a type predicate over a module union)
function looksLikeGenFile(x: unknown): x is GenFile {
	const v = x as any;
	return (
		!!v &&
		typeof v === 'object' &&
		v.kind === 'file' &&
		typeof v.name === 'string'
	);
}

function collectFiles(): GenFile[] {
	const files: GenFile[] = [];
	const mods = [
		Auth,
		Authorization,
		Comment,
		Content,
		CreatorDashboard,
		Generic,
		Notification,
		Page,
		Settings,
		FragmentsRoot,
	];
	for (const m of mods) {
		for (const value of Object.values(m) as unknown[]) {
			if (looksLikeGenFile(value)) files.push(value);
		}
	}
	return files;
}

const registry: Registry = createRegistry(...collectFiles());

/** Create a protovalidate Validator bound to this package’s descriptors. */
export async function getValidator(): Promise<Validator> {
	return createValidator({ registry });
}
