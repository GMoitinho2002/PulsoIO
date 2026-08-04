import { FormControl } from '@angular/forms';
import { evaluatePasswordRequirements, passwordPolicyValidator } from './admin-user.models';

describe('política de senha de usuários', () => {
  it('aceita seis caracteres com maiúscula, minúscula e pontuação sem exigir número', () => {
    const requirements = evaluatePasswordRequirements('Abcde!');

    expect(requirements).toEqual({
      minLength: true,
      uppercase: true,
      lowercase: true,
      special: true
    });
    expect(passwordPolicyValidator(new FormControl('Abcde!'))).toBeNull();
  });

  it('aceita símbolos Unicode como caractere especial', () => {
    expect(evaluatePasswordRequirements('Ábcde€').special).toBe(true);
  });

  it.each(['Abcdef', 'Abcd1f', 'Abcd f', `Abcd${'\u0301'}f`])(
    'não considera letras, números, espaço ou marca combinante como especial: %s',
    password => {
      expect(evaluatePasswordRequirements(password).special).toBe(false);
      expect(passwordPolicyValidator(new FormControl(password))).not.toBeNull();
    }
  );

  it('informa cada requisito ainda não atendido', () => {
    expect(evaluatePasswordRequirements('abc!')).toEqual({
      minLength: false,
      uppercase: false,
      lowercase: true,
      special: true
    });
  });
});
