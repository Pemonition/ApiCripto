using Carteira.Domain.Consultas;
using FluentAssertions;

namespace Carteira.Tests;

/// <summary>
/// A sanitização da paginação é a primeira linha de defesa contra uma consulta
/// que derruba a API. Vale testar porque os valores vêm da URL — ou seja, de
/// qualquer um.
/// </summary>
public class PaginacaoTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Pagina_nunca_deve_ficar_abaixo_de_um(int informado, int esperado)
    {
        new AtivoFiltro { Pagina = informado }.Pagina.Should().Be(esperado);
    }

    [Theory]
    [InlineData(0, AtivoFiltro.TamanhoPaginaPadrao)]
    [InlineData(-1, AtivoFiltro.TamanhoPaginaPadrao)]
    [InlineData(50, 50)]
    [InlineData(999_999, AtivoFiltro.TamanhoPaginaMaximo)]
    public void TamanhoPagina_deve_respeitar_padrao_e_teto(int informado, int esperado)
    {
        new AtivoFiltro { TamanhoPagina = informado }.TamanhoPagina.Should().Be(esperado);
    }

    [Fact]
    public void Pular_deve_ser_derivado_da_pagina_e_do_tamanho()
    {
        var filtro = new AtivoFiltro { Pagina = 3, TamanhoPagina = 25 };
        filtro.Pular.Should().Be(50);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(7, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(100, 3, 34)]
    public void TotalPaginas_deve_arredondar_para_cima(int total, int tamanho, int esperado)
    {
        PagedResult<string>.Criar(Array.Empty<string>(), total, 1, tamanho)
            .TotalPaginas.Should().Be(esperado);
    }

    [Fact]
    public void TemProxima_e_TemAnterior_devem_refletir_a_posicao_na_sequencia()
    {
        var primeira = PagedResult<string>.Criar(Array.Empty<string>(), total: 100, pagina: 1, tamanhoPagina: 20);
        primeira.TemAnterior.Should().BeFalse();
        primeira.TemProxima.Should().BeTrue();

        var ultima = PagedResult<string>.Criar(Array.Empty<string>(), total: 100, pagina: 5, tamanhoPagina: 20);
        ultima.TemAnterior.Should().BeTrue();
        ultima.TemProxima.Should().BeFalse();
    }
}
